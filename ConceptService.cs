using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
//using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using ConceptAccessService;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RaynorJdeApi;
using RaynorJdeApi.Models;
using MailKit.Net;
using MimeKit;
using MailKit.Net.Smtp;
using System.Drawing;

public class ConceptService : IConceptService
{
    private string logfile;
    private string EWOrderNum;
    private string emailIT;
    private string emailSpr;
    private string emailJdeBom;
    private string jdeTableBase;
    private string logPath;
    private readonly string createPOFolder;
    private readonly string _environment;
    private readonly HttpClient httpClient;
    private readonly AccessToken token;
    private readonly IConfiguration _configuration;
    private readonly Database _db;
    private readonly Database _database;
    private readonly Database _syspro;
    private readonly Database _jde;
    private readonly JdeDate _jdeDate;
    private ConceptAccessService.Order order1;

    private string strOrderStatus = "";
    private  string strReasonCodes = "";
    private string strOrderStatusPre = "";
    private string strOrderType = "";
    private string strSPR;

    public ConceptService(IConfiguration configuration)
    {
        // Get values from the appsettings json
        _configuration = configuration;
        logPath = _configuration.GetValue<string>("AppSettings:LogPath");
        jdeTableBase = _configuration.GetValue<string>("AppSettings:JdeTableBase");
        _db = new Database(_configuration.GetValue<string>("ConnectionStrings:CustomDatabase"), "", true);
        _database = new Database(_configuration.GetValue<string>("ConnectionStrings:ConceptDatabase"), "", true);
        _syspro = new Database(_configuration.GetValue<string>("ConnectionStrings:SysproDatabase"), "", true);
        _jde = new Database(_configuration.GetValue<string>("ConnectionStrings:JdeDatabase"), jdeTableBase, false);
        _environment = _configuration.GetValue<string>("AppSettings:EasyWebEnv");
        createPOFolder = _configuration.GetValue<string>("AppSettings:CreatePOFolder");
        emailIT = _configuration.GetValue<string>("AppSettings:EmailIT");
        emailSpr = _configuration.GetValue<string>("AppSettings:EmailSPR");
        emailJdeBom = _configuration.GetValue<string>("AppSettings:EmailJdeBom");
        order1 = null;
        _jdeDate = new JdeDate();

        // Create bearer token for web requests
        string apiUrl = _configuration.GetValue<string>("AppSettings:ApiUrl3"); // Main url for JDE web api
        token = GetAccessToken(apiUrl, "", "orc02", "orc02");
        //   token = GetAccessToken(apiUrl, "", "rwadmin1", "rwag");
        // Initialize http web request
        httpClient = new HttpClient();
        //commented on 8/29
        //  httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.Default.GetBytes("orc02:orc02")));
        httpClient.Timeout = new TimeSpan(0, 3, 0);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.Add("jde-AIS-Auth", token.userInfo.token);

    }

    // Soap end-point for EasyWeb to call order is submitted
    public async Task<FireOrderReturn> fireOrder(string in0)
    {
        // Set log file path and name
        logfile = logPath + in0.Replace(" ", "") + "_" + DateTime.Now.ToString("MMddyyyymmssfff") + ".txt";
        WriteLog("Start");
        // Get Easyweb order number to process
        in0 = in0.Trim();
        EWOrderNum = in0;
        string apiUrl = _configuration.GetValue<string>("AppSettings:ApiUrl"); // Main url for JDE web api
        string apiUrl2 = _configuration.GetValue<string>("AppSettings:ApiUrl2"); // JDE web api url for saving text
        ConceptAccessAPIClient conceptAccess = new ConceptAccessAPIClient();
        if (_environment == "dev")
        {
            // Easyweb endpoint for development
            conceptAccess.Endpoint.Address = new System.ServiceModel.EndpointAddress(_configuration.GetValue<string>("AppSettings:EasyWebEnvDev"));
        }
        else if(_environment == "test")
        {
            conceptAccess.Endpoint.Address = new System.ServiceModel.EndpointAddress(_configuration.GetValue<string>("AppSettings:EasyWebEnvTest"));
        }
        else
        {
            // Easyweb endpoint for production
            conceptAccess.Endpoint.Address = new System.ServiceModel.EndpointAddress(_configuration.GetValue<string>("AppSettings:EasyWebEnvProd"));
            conceptAccess.Endpoint.Binding.SendTimeout = TimeSpan.FromMinutes(10);
            

        }

        ConceptAccessService.Order order = await conceptAccess.getOrderAsync(in0); // Get order details from EasyWeb

        order1 = order;

        try
        {
            strOrderStatusPre = _database.GetField("select  DESIGN_INPUT_VAL from CO_DES_INPUT where DESIGN_ID='" + order1.Detail[0].ID + "' and INPUT_NAME = 'GL_ORDER_STATUS1'");
            strReasonCodes = _database.GetField("select  DESIGN_INPUT_VAL from CO_DES_INPUT where DESIGN_ID='" + order1.Detail[0].ID + "' and INPUT_NAME = 'GL_REASON_CODES1'");
            strOrderType = _database.GetField("select  DESIGN_INPUT_VAL from CO_DES_INPUT where DESIGN_ID='" + order1.Detail[0].ID + "' and INPUT_NAME = 'Order_Type'");
            strSPR = _database.GetField("select  DESIGN_INPUT_VAL from CO_DES_INPUT where DESIGN_ID='" + order1.Detail[0].ID + "' and INPUT_NAME = 'SPR'");

           

            strOrderStatus = strOrderStatusPre == "" ? "SO" : strOrderStatusPre;


            if (strOrderStatus == "SO")
            {
                strReasonCodes = " ";
            }
        }
        catch { }
        //endys


        WriteLog("Retrieved order from EasyWeb - " + order.ORDER_NUM);
        // Create internal structure for all of the order details
        EWOrder ewOrder = await CreateEWOrder(conceptAccess, order, apiUrl);
        bool processed = false;
        // Check for customer po and a valid bill to and ship to and stop processing if empty
        // YS - UNCOMMENT AFTER TEST
        if (!string.IsNullOrWhiteSpace(ewOrder.CustomerPo) && !string.IsNullOrWhiteSpace(ewOrder.CustomerCode) && ewOrder.CustomerCode.StartsWith("8") && !string.IsNullOrWhiteSpace(ewOrder.ShipTo) && ewOrder.ShipTo.StartsWith("8") && ewOrder.ShipDate != "error")
       //  if (!string.IsNullOrWhiteSpace(ewOrder.CustomerPo))
        {
            OrderInfo orderInfo = null;
            // Get next order document, batch and invoice numbers from JDE
            string result = SendWebApiMessage(apiUrl + "N554752", "").Result;
            if (result != null)
            {
                orderInfo = JsonConvert.DeserializeObject<OrderInfo>(result);
                ewOrder.SerialNum = in0;
                //ewOrder.OrderType = strOrderType;
                //ewOrder.SPR = strSPR;
                ewOrder.DocumentNum = orderInfo.mnEdiDocumentNumber; // Add the returned document number from JDE to the order
                ewOrder.SalesOrder = orderInfo.mnDocumentOrderInvoiceE; // Add the returned invoice number from JDE to the order
                
            }


            // Process order in JDE
            ProcessEWOrder(conceptAccess, ewOrder, in0, orderInfo);
            if (string.IsNullOrWhiteSpace(ewOrder.Error)) // Check for errors generated when processing order
            {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                Task.Run(async () =>
                {
                    await Task.Delay(6000);
                    conceptAccess.updateOrderAsync(in0, ewOrder.SalesOrder, "FM");
                });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                processed = true;


                // Update custom table with glazing data
                var keyvalue = _jde.GetField("select PCUKID FROM CRPDTA.F574802 order by PCUKID desc");
                var key = 0;
                if (ewOrder.OrderType == "R")
                {
                    if (!string.IsNullOrWhiteSpace(keyvalue))
                    {
                        key = Convert.ToInt32(keyvalue);
                        foreach (var item in ewOrder.DoorItems)
                        {
                            SetGlazingData(++key, ewOrder.SalesOrder, item.GLZ_CODE_SB_RP_1, item.CNC_SB_RP1, item.PC_SB_RP1, item.SB_RP_1_PANEL_TYPE, item.SB_RP_1_ORPHAN, ewOrder.OrderType,item.SB_RP1_PANEL_CONFIGURATION, item.SB_RP_1_DOOR_MODEL, item.SB_RP_1_PANEL_STYLE, item.SB_RP_1_DOOR_COLOUR, item.SB_RP_1_DRILL_FOR_HINGES, item.SB_RP_1_DRILL_CODE, item.SB_RP_1_GLAZED, item.SB_RP_1_BOTTOM_RTNR_SEAL, item.SB_RP_1_END_CAP, item.SB_RP_1_PANEL_SEQUENCE, item.SB_RP_1_SMART_COM_CODE, item.SB_RP_1_DF_SEQ, item.SB_RP_1_WIDTH_CODE);
                            SetGlazingData(++key, ewOrder.SalesOrder, item.GLZ_CODE_SB_RP_2, item.CNC_SB_RP2, item.PC_SB_RP2, item.SB_RP_2_PANEL_TYPE, item.SB_RP_2_ORPHAN, ewOrder.OrderType,item.SB_RP2_PANEL_CONFIGURATION, item.SB_RP_2_DOOR_MODEL, item.SB_RP_2_PANEL_STYLE, item.SB_RP_2_DOOR_COLOUR, item.SB_RP_2_DRILL_FOR_HINGES, item.SB_RP_2_DRILL_CODE, item.SB_RP_2_GLAZED, item.SB_RP_2_BOTTOM_RTNR_SEAL, item.SB_RP_2_END_CAP, item.SB_RP_2_PANEL_SEQUENCE, item.SB_RP_2_SMART_COM_CODE, item.SB_RP_2_DF_SEQ, item.SB_RP_2_WIDTH_CODE);
                            SetGlazingData(++key, ewOrder.SalesOrder, item.GLZ_CODE_INT_1_RP_1, item.CNC_INT1_RP1, item.PC_INT1_RP1, item.INT1_RP1_PANEL_TYPE, item.INT1_RP_1_ORPHAN, ewOrder.OrderType,item.INT1_RP1_PANEL_CONFIGURATION,item.INT1_RP_1_DOOR_MODEL,item.INT1_RP_1_PANEL_STYLE,item.INT1_RP_1_DOOR_COLOUR,item.INT1_RP_1_DRILL_FOR_HINGES,item.INT1_RP_1_DRILL_CODE,item.INT1_RP_1_GLAZED,item.INT1_RP_1_BOTTOM_RTNR_SEAL,item.INT1_RP_1_END_CAP,item.INT1_RP_1_PANEL_SEQUENCE,item.INT1_RP_1_SMART_COM_CODE,item.INT1_RP_1_DF_SEQ,item.INT1_RP_1_WIDTH_CODE);
                            SetGlazingData(++key, ewOrder.SalesOrder, item.GLZ_CODE_INT_1_RP_2, item.CNC_INT1_RP2, item.PC_INT1_RP2, item.INT1_RP2_PANEL_TYPE, item.INT1_RP_2_ORPHAN, ewOrder.OrderType,item.INT1_RP2_PANEL_CONFIGURATION,item.INT1_RP_2_DOOR_MODEL,item.INT1_RP_2_PANEL_STYLE,item.INT1_RP_2_DOOR_COLOUR,item.INT1_RP_2_DRILL_FOR_HINGES,item.INT1_RP_2_DRILL_CODE,item.INT1_RP_2_GLAZED,item.INT1_RP_2_BOTTOM_RTNR_SEAL,item.INT1_RP_2_END_CAP,item.INT1_RP_2_PANEL_SEQUENCE,item.INT1_RP_2_SMART_COM_CODE,item.INT1_RP_2_DF_SEQ,item.INT1_RP_2_WIDTH_CODE);
                            SetGlazingData(++key, ewOrder.SalesOrder, item.GLZ_CODE_INT_2_RP_1, item.CNC_INT2_RP1, item.PC_INT2_RP1, item.INT2_RP1_PANEL_TYPE, item.INT2_RP_1_ORPHAN, ewOrder.OrderType,item.INT2_RP1_PANEL_CONFIGURATION,item.INT2_RP_1_DOOR_MODEL,item.INT2_RP_1_PANEL_STYLE,item.INT2_RP_1_DOOR_COLOUR,item.INT2_RP_1_DRILL_FOR_HINGES,item.INT2_RP_1_DRILL_CODE,item.INT2_RP_1_GLAZED,item.INT2_RP_1_BOTTOM_RTNR_SEAL,item.INT2_RP_1_END_CAP,item.INT2_RP_1_PANEL_SEQUENCE,item.INT2_RP_1_SMART_COM_CODE,item.INT2_RP_1_DF_SEQ,item.INT2_RP_1_WIDTH_CODE);
                            SetGlazingData(++key, ewOrder.SalesOrder, item.GLZ_CODE_INT_2_RP_2, item.CNC_INT2_RP2, item.PC_INT2_RP2, item.INT2_RP2_PANEL_TYPE, item.INT2_RP_2_ORPHAN, ewOrder.OrderType,item.INT2_RP2_PANEL_CONFIGURATION,item.INT2_RP_2_DOOR_MODEL,item.INT2_RP_2_PANEL_STYLE,item.INT2_RP_2_DOOR_COLOUR,item.INT2_RP_2_DRILL_FOR_HINGES,item.INT2_RP_2_DRILL_CODE,item.INT2_RP_2_GLAZED,item.INT2_RP_2_BOTTOM_RTNR_SEAL,item.INT2_RP_2_END_CAP,item.INT2_RP_2_PANEL_SEQUENCE,item.INT2_RP_2_SMART_COM_CODE,item.INT2_RP_2_DF_SEQ,item.INT2_RP_2_WIDTH_CODE);
                            SetGlazingData(++key, ewOrder.SalesOrder, item.GLZ_CODE_INT_3_RP_1, item.CNC_INT3_RP1, item.PC_INT3_RP1, item.INT3_RP1_PANEL_TYPE, item.INT3_RP_1_ORPHAN, ewOrder.OrderType,item.INT3_RP1_PANEL_CONFIGURATION,item.INT3_RP_1_DOOR_MODEL,item.INT3_RP_1_PANEL_STYLE,item.INT3_RP_1_DOOR_COLOUR,item.INT3_RP_1_DRILL_FOR_HINGES,item.INT3_RP_1_DRILL_CODE,item.INT3_RP_1_GLAZED,item.INT3_RP_1_BOTTOM_RTNR_SEAL,item.INT3_RP_1_END_CAP,item.INT3_RP_1_PANEL_SEQUENCE,item.INT3_RP_1_SMART_COM_CODE,item.INT3_RP_1_DF_SEQ,item.INT3_RP_1_WIDTH_CODE);
                            SetGlazingData(++key, ewOrder.SalesOrder, item.GLZ_CODE_INT_3_RP_2, item.CNC_INT3_RP2, item.PC_INT3_RP2, item.INT3_RP2_PANEL_TYPE, item.INT3_RP_2_ORPHAN, ewOrder.OrderType,item.INT3_RP2_PANEL_CONFIGURATION,item.INT3_RP_2_DOOR_MODEL,item.INT3_RP_2_PANEL_STYLE,item.INT3_RP_2_DOOR_COLOUR,item.INT3_RP_2_DRILL_FOR_HINGES,item.INT3_RP_2_DRILL_CODE,item.INT3_RP_2_GLAZED,item.INT3_RP_2_BOTTOM_RTNR_SEAL,item.INT3_RP_2_END_CAP,item.INT3_RP_2_PANEL_SEQUENCE,item.INT3_RP_2_SMART_COM_CODE,item.INT3_RP_2_DF_SEQ,item.INT3_RP_2_WIDTH_CODE);
                        }
                    }
                    else
                    {
                        WriteLog("Could not get key value from the custom table");
                    }
                }
                 if (ewOrder.OrderType == "C")                
                 {
                        if (!string.IsNullOrWhiteSpace(keyvalue))
                        {
                            key = Convert.ToInt32(keyvalue);
                            foreach (var item in ewOrder.DoorItems)
                            {
                                SetCommGlazingData(++key, ewOrder.SalesOrder, item.SEC_1_SEC_BDL_RP, item.SEC_1, item.SEC_1_PANEL_QTY, item.PANEL_CONFIGURATION_SEC_1, ewOrder.OrderType);
                                SetCommGlazingData(++key, ewOrder.SalesOrder, item.SEC_2_SEC_BDL_RP, item.SEC_2, item.SEC_2_PANEL_QTY, item.PANEL_CONFIGURATION_SEC_2, ewOrder.OrderType);
                                SetCommGlazingData(++key, ewOrder.SalesOrder, item.SEC_3_SEC_BDL_RP, item.SEC_3, item.SEC_3_PANEL_QTY, item.PANEL_CONFIGURATION_SEC_3, ewOrder.OrderType);
                                SetCommGlazingData(++key, ewOrder.SalesOrder, item.SEC_4_SEC_BDL_RP, item.SEC_4, item.SEC_4_PANEL_QTY, item.PANEL_CONFIGURATION_SEC_4, ewOrder.OrderType);
                                SetCommGlazingData(++key, ewOrder.SalesOrder, item.SEC_5_SEC_BDL_RP, item.SEC_5, item.SEC_5_PANEL_QTY, item.PANEL_CONFIGURATION_SEC_5, ewOrder.OrderType);
                                SetCommGlazingData(++key, ewOrder.SalesOrder, item.SEC_6_SEC_BDL_RP, item.SEC_6, item.SEC_6_PANEL_QTY, item.PANEL_CONFIGURATION_SEC_6, ewOrder.OrderType);
                                SetCommGlazingData(++key, ewOrder.SalesOrder, item.SEC_7_SEC_BDL_RP, item.SEC_7, item.SEC_7_PANEL_QTY, item.PANEL_CONFIGURATION_SEC_7, ewOrder.OrderType);
                                SetCommGlazingData(++key, ewOrder.SalesOrder, item.SEC_8_SEC_BDL_RP, item.SEC_8, item.SEC_8_PANEL_QTY, item.PANEL_CONFIGURATION_SEC_8, ewOrder.OrderType);
                                SetCommGlazingData(++key, ewOrder.SalesOrder, item.SEC_9_SEC_BDL_RP, item.SEC_9, item.SEC_9_PANEL_QTY, item.PANEL_CONFIGURATION_SEC_9, ewOrder.OrderType);
                                SetCommGlazingData(++key, ewOrder.SalesOrder, item.SEC_10_SEC_BDL_RP,item.SEC_10,item.SEC_10_PANEL_QTY,item.PANEL_CONFIGURATION_SEC_10,ewOrder.OrderType);
                                SetCommGlazingData(++key, ewOrder.SalesOrder, item.SEC_BTM_SEC_BDL_RP,item.SEC_BTM,item.SEC_BTM_PANEL_QTY,item.PANEL_CONFIGURATION_SEC_BTM,ewOrder.OrderType);
                            }
                        }
                        else
                        {
                            WriteLog("Could not get key value from the custom table");
                        }
                  }                               
            }
            else
            {
                // Email for processing errors
                SendMail("JDE Order Submission Error for EW Order " + EWOrderNum, ewOrder.Error, emailIT);
            }
        }
        // If order has configuration get check the errors and send email with appropriate text in the body
        string body = "";
        if (string.IsNullOrWhiteSpace(ewOrder.CustomerPo))
        {
            body = "No Customer PO";
        }
        if (string.IsNullOrWhiteSpace(ewOrder.CustomerCode) || !ewOrder.CustomerCode.StartsWith("8") || string.IsNullOrWhiteSpace(ewOrder.ShipTo) || !ewOrder.ShipTo.StartsWith("8"))
        {
            if (body != "") body += " and ";
            body += "Bill To and/or Ship To invalid";
        }
        if (ewOrder.ShipDate == "error")
        {
            if (body != "") body += " and ";
            body += "Ship date invalid";
        }
        if (body != "")
        {
            SendMail("JDE Order Submission Error for EW Order " + EWOrderNum, body, emailIT);
        }
        WriteLog("Completed processing of EW Sales Order " + ewOrder.SalesOrder);
        return new FireOrderReturn() { success = processed.ToString(), key = "", message = "" };
    }
    

    //revised with CNC Code
    private void SetGlazingData(int key, string salesorder, string data, string data1, string data2, string data3, string data4, string data5, string data6, string data7,string data8, string data9,string data10,string data11,string data12,string data13,string data14,string data15,string data16,string data17,string data18)
    {
        if (!string.IsNullOrWhiteSpace(data))
        {
            var date = _jdeDate.GregorianToJulian(DateTime.Now);
            var time = _jdeDate.TimeOfDayToInt(DateTime.Now.TimeOfDay.Ticks);
            var sqlbase = "insert into CRPDTA.F574802 (PCUKID,PCCO,PCDOCO,PCKCOO,PC57DOCO,PCDCTO,PCLNID,PC57CNC,PC57GLZLC,PC57PC,PC57PKGC,PC57DC,PC57SCC,PC57BBC,PCDTA1,PCDTA2,PCDTA3,PCDTA4,PCFLAG,PCEV01,PCJOBN,PCEV02,PCEV03,PC74UDTE1,PC74UDTE2,PCMATH01,PCMATH02,PCDL03,PCDL02,PCDL01,PCLITM,PC57GLZC,PCUSER,PCPID,PCUPMJ,PCTDAY,PC57OP,PCEV04,PC57PGL,PCMNAM,PC57PS,PC57DCOL,PC57DR,PC57DDESC,PC57PGLZ,PC57BRET,PC57PEC,PC57PSEQ,PC57WC,PC57PPSB) ";
            sqlbase += "VALUES({0},'', 0,'00500'," + salesorder + ",'',0,'{5}','','{6}','','{15}','{20}','','','','','','','','','','{7}',0,0,0,0,'','{1}','{2}','{3}','{4}','WEBORDER','WEBORDER'," + date + "," + time.ToString() + ",'{8}','{9}','{10}','{11}','{12}','{13}','{14}','','{16}','{17}','{18}','{19}','{21}','')";


            var values = data.Split(',');
            //var CNCvalues = data1.Split(',');
            var CNCvalues1toend = "";
             if (!string.IsNullOrEmpty(data1))
             {
                 var CNCvalues = data1.Split(',');
                 // Ensure there's at least one value in CNCvalues before looping
                 if (CNCvalues.Length > 1)
                 {
                     for (int j = 1; j < CNCvalues.Length; j++)
                     {
                         CNCvalues1toend += CNCvalues1toend != "" ? "," : "";
                         CNCvalues1toend += CNCvalues[j];
                     }
                 }
             }
            var Panelcode = "";
            var PanelType = "";
            var values3toend = "";
            //var CNCvalues1toend = "";
            var Orphan = "";
            var OrderType = data5;
            var PanelGlazing = data6;
            var DoorModel = data7;
            var PanelStyle = data8;
            var DoorColour = data9;
            var DrillForHinges = data10;
            var DrillCode = data11;
            var Glazed = data12;
            var BottomSealRtnr = data13;
            var EndCap = data14;
            var PanelSequence = data15;
            var SmartComeCode = data16;
            var DFSeq = data17;
            var WidthCode = data18;

             
            if (string.IsNullOrEmpty(data1))
            {
                Panelcode = "";
            }
            else
            {
                Panelcode = data2;
            }

            PanelType = data3;
            if(string.IsNullOrEmpty(data4))
            {
                Orphan = "";
            }
            else
            {
                Orphan = data4;
            }
            
            for (int i = 3; i < values.Length; i++)
            {
                values3toend += values3toend != "" ? "," : "";
                values3toend += values[i];
            }

            /*for(int j = 1; j < CNCvalues.Length; j++)
            {
                CNCvalues1toend += CNCvalues1toend != "" ? "," : "";
                CNCvalues1toend += CNCvalues[j];
            }*/

            var args = new string[] { key.ToString(), values[0], values[1], values[2], values3toend, CNCvalues1toend,Panelcode,PanelType,Orphan,OrderType,PanelGlazing,DoorModel,PanelStyle,DoorColour,DrillForHinges,DrillCode,Glazed,BottomSealRtnr,EndCap,PanelSequence,SmartComeCode,WidthCode};
            var sql = string.Format(sqlbase, args);
            _jde.ExecuteCommand(sql);
            WriteLog(sql);
            if (_jde.Error != "")
            {
                WriteLog(sql);
                WriteLog(_jde.Error);
            }
        }
    }

    private void SetCommGlazingData(int key, string salesorder, string data, string data1, string data2, string data3, string data4)
    {
        if (!string.IsNullOrWhiteSpace(data))
        {
            var date = _jdeDate.GregorianToJulian(DateTime.Now);
            var time = _jdeDate.TimeOfDayToInt(DateTime.Now.TimeOfDay.Ticks);
            var sqlbase = "insert into CRPDTA.F574802 (PCUKID,PCCO,PCDOCO,PCKCOO,PC57DOCO,PCDCTO,PCLNID,PC57CNC,PC57GLZLC,PC57PC,PC57PKGC,PC57DC,PC57SCC,PC57BBC,PCDTA1,PCDTA2,PCDTA3,PCDTA4,PCFLAG,PCEV01,PCJOBN,PCEV02,PCEV03,PC74UDTE1,PC74UDTE2,PCMATH01,PCMATH02,PCDL03,PCDL02,PCDL01,PCLITM,PC57GLZC,PCUSER,PCPID,PCUPMJ,PCTDAY,PC57OP,PCEV05,PCEV04,PC57PGL) ";
            sqlbase += "VALUES({0},'', 0,'00500'," + salesorder + ",'',0,'{3}','','','','','','','','','','','','','','','',0,0,0,0,'','{1}','','{2}','','WEBORDER','WEBORDER'," + date + "," + time.ToString() + ",0,'{5}','{4}','{6}')";

            var values = data.Split(',');
            var CNCvalues = data1;
            var PanelQty = data2;
            var OrderType = data4;
            var PanelGlazing = data3;

            var args = new string[] { key.ToString(), values[0], values[1], CNCvalues, OrderType,PanelQty,PanelGlazing };
            var sql = string.Format(sqlbase, args);
            _jde.ExecuteCommand(sql);
            if (_jde.Error != "")
            {
                WriteLog(sql);
                WriteLog(_jde.Error);
            }

        }
    }



    // Process EasyWeb order to create an internal order structure
    private async Task<EWOrder> CreateEWOrder(ConceptAccessAPI conceptAccess, Order order, string apiUrl)
    {
        string reference;
        // Set item values to determine for valid Y lines, parts & prices to add and discount items
        string[] validYLineParts = _configuration.GetValue<string>("AppSettings:ValidYLineParts").Split(',');
        string[] partPrefix = _configuration.GetValue<string>("AppSettings:PartDescPrefix").Split(',');
        string[] adderPrefix = _configuration.GetValue<string>("AppSettings:PriceAdderPrefix").Split(',');
        string discountItemNum = _configuration.GetValue<string>("AppSettings:DiscountItemNum");
        ItemMaster itemMaster = new ItemMaster();
        // Initialize the internal order stucture
        // Changed CustomerCode = order.ACCOUNT_NUM to CustomerCode = BILL_TO_CONTACT_NAME on June 21, 2022
        EWOrder ewOrder = new EWOrder()
        {
            CustomerPo = "",
            OrderType = "",
            ShipDate = "",
            JobTag = "",
            ShipVia = order.SHIP_VIA,
            SerialNum = order.SERIAL_NUMBER,
            DoorItems = new List<DoorItem>(),
            Error = "",
            ConfigReference = "",
            QuoteReference = "",
            Country = order.BILL_TO_COUNTRY,
            CustomerCode = order.BILL_TO_CONTACT_NAME, //- changed on August 11, 2022
            //CustomerCode = !int.TryParse(order.ACCOUNT_NUM, out int _) ? _jde.GetField("select ABAN82 from CRPDTA.F0101 where ABMCU='       50000' and ABAT1='C' and ABURRF='" + order.ACCOUNT_NUM + "'") : order.BILL_TO_CONTACT_NAME,
            ShipTo = order.SHIP_TO_CONTACT_NAME,
            //ShipTo = _jde.GetField("select ABAN8 from CRPDTA.F0101 where ABMCU='       50000' and ABAT1='C' and ABURRF='" + order.SHIP_TO_CONTACT_NAME + "'"),
            //ShipTo = !int.TryParse(order.SHIP_TO_CONTACT_NAME, out int _) ? _jde.GetField("select ABAN8 from CRPDTA.F0101 where ABMCU='       50000' and ABAT1='C' and ABURRF='" + order.SHIP_TO_CONTACT_NAME + "'") : order.SHIP_TO_CONTACT_NAME,
            SysproSalesOrder = order.ORDER_REF_NUM,
            CreatePO = false,
            Var25 = new List<string>(),
            Var25Item = new List<OrderItem>(),
            ExcludedItem = new List<string>(),
            Submitted = false
        };
        ewOrder.UserId = _database.GetField("SELECT USER_NAME FROM CO_USER WHERE USER_ID='" + order.USER_ID + "'"); // Get the user id for the order submission
        string altdate = "";
        DateTime date;
        List<string> comments = new List<string>();
        // Get the EasyWeb input data required by JDE   
        foreach (var item in order.Input)
        {
            switch (item.name)
            {
                case "REQ_SHIP_DATE":
                    try
                    {
                        date = Convert.ToDateTime(item.Value[0].label);
                        if (date.Date < DateTime.Now.AddDays(1).Date)
                        {
                            ewOrder.ShipDate = "error";
                        }
                        else
                        {
                            ewOrder.ShipDate = date.ToString("MM/dd/yyyy");
                        }
                    }
                    catch
                    {
                        ewOrder.ShipDate = "error";
                    }
                    break;
                case "USE_ALT_DATE":
                    altdate = item.Value[0].label;
                    break;
                case "ALT_DATE":
                    if (altdate == "YES")
                    {
                        try
                        {
                            date = Convert.ToDateTime(item.Value[0].label);
                            if (date.Date < DateTime.Now.AddDays(1).Date)
                            {
                                ewOrder.ShipDate = "error";
                            }
                            else
                            {
                                ewOrder.ShipDate = date.ToString("MM/dd/yyyy");
                            }
                        }
                        catch
                        {
                            ewOrder.ShipDate = "error";
                        }
                    }
                    break;
                case "CUSTOMER_PO":
                    ewOrder.CustomerPo = item.Value[0].id;
                    break;
                case "ORDER_TYPE":
                    ewOrder.OrderType = item.Value[0].id;
                    break;
                case "TAG":
                    ewOrder.JobTag = item.Value[0].id;
                    break;
            }
        }
        OrderItem orderItem1;
        OrderItem orderItem2;
        OrderItem orderItem3;
        LotNumberReturn lot;
        Configuration configuration = null;
        DoorItem doorItem = null;
        string result;
        string optionacc = "";
        string lotnumber = "";
        string linetype;
        string easyweblinetype;
        string inputitem;
        string inputvalue;
        int line = 1;
        int linenum;
        int doornum = 0;
        int inputnum;
        bool lotflag = false;
        foreach (var detail in order.Detail)
        {
            if (detail.TYPE == "C") // Only process EasyWeb order data for Type C
            {
                // Initialize the internal structure with door structures 
                doorItem = new DoorItem()
                {
                    Items = new List<OrderItem>(),
                    WindowQuantity = 0,
                    HoldOrdersCode = " ",
                    GLDoorColour = "",
                    GLDoorSize = "",
                    GLDoorModel = "",
                    GLEndCaps = "",
                    GLLiftType = "",
                    GLHlAmt = "",
                    GLNumberOfSection = "",
                    GLStyle = "",
                    GLTopWeatherSeal = "",
                    GLBottomSeal = "",
                    GLTrussStyle = "",
                    GLGlazingType = "",
                    GlALumGlazingType="",
                    GL_ALUM_BTM_SEC_TYPE="",               
                    GLWindowType = "",
                    GLGlassType = "",
                    GLFrameColour = "",
                    GLLitesPerSpacing = "",
                    GLSpacing = "",
                    GLHardwareSize = "",
                    GLMountType = "",
                    GLJamb = "",
                    GLShaftType = "",
                    GLSpringRH = "",
                    GLSpringLH = "",
                    GLExtensionSpring = "",
                    GLMediaAttachmentSB = "",
                    MA_SB_BTM = "",
                    MA_SB_GLZ = "",
                    MA_SB_INT_1 = "",
                    MA_SB_INT_2 = "",
                    //  GLOrderStatus = "",
                    //  GLReasonCodes = ""
                };
                ewOrder.DoorItems.Add(doorItem);

                foreach (var master in detail.ItemMaster)
                {
                    if (master.SMARTPART_NUM.StartsWith("C-")) // Add main order descriptions
                    {
                        comments.Add(master.DESCRIPTION.Length <= 30 ? master.DESCRIPTION : master.DESCRIPTION.Substring(0, 30));
                        reference = master.SMARTPART_NUM.Substring(0, master.SMARTPART_NUM[2..].IndexOf('-') + 2);
                        if ((reference.Length + ewOrder.ConfigReference.Length) <= 30)
                            ewOrder.ConfigReference += !string.IsNullOrEmpty(ewOrder.ConfigReference) ? "," + reference : reference;
                    }
                    if (master.ITEM_NUM == "Tag")
                    {
                        doorItem.OrderTag = master.DESCRIPTION.Replace("Tag: ", "");
                    }
                }

                // Get Glazing Routing Notes and windows part number if description is LM_GLAZING and LM_NOTES are in the params
                if (detail.Routing != null)
                {
                    foreach (var routing in detail.Routing)
                    {
                        if (routing.Operation != null)
                        {
                            foreach (var operation in routing.Operation)
                            {
                                if (operation.DESCRIPTION.Contains("GLAZING"))
                                {
                                    foreach (var param in operation.OperationParam)
                                    {
                                        if (param.NAME.Contains("NOTES"))
                                        {
                                            doorItem.GlazingNotes = param.VALUE;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                

                // If the main part number is also in the bom add bom item descriptions
                if (detail.SMARTPART_NUM == detail.Bom.SMARTPART_NUM)
                {
                    foreach (var bom in detail.Bom.Bom1)
                    {
                        if (bom.SMARTPART_NUM != "Tag")
                        {
                            itemMaster = detail.ItemMaster.Where(x => x.SMARTPART_NUM == bom.SMARTPART_NUM).FirstOrDefault();
                            if (itemMaster != null)
                            {
                                if (bom.SMARTPART_NUM != "OPTIONS_ACCESS_HEADER" && bom.SMARTPART_NUM != "DOOR_HARDWARE_HEADER")
                                {
                                    comments.Add(itemMaster.DESCRIPTION.Length <= 30 ? itemMaster.DESCRIPTION : itemMaster.DESCRIPTION.Substring(0, 30));
                                }
                            }
                        }
                        else
                        {
                        }
                    }
                }
                // Add order project, order number and descriptions compiled in the above code to the internal structure
                if (!string.IsNullOrWhiteSpace(order.PROJECT))
                {
                    doorItem.Items.Add(new OrderItem() { ItemNum = "", UOM = "", Description = order.PROJECT, LineNum = 0, LineType = "", LotNumber = "", Taxable = "" });
                }
                if (!string.IsNullOrWhiteSpace(order.ORDER_NUM))
                {
                    doorItem.Items.Add(new OrderItem() { ItemNum = "", UOM = "", Description = order.ORDER_NUM, LineNum = 0, LineType = "", LotNumber = "", Taxable = "" });
                }
                foreach (var comment in comments)
                {
                    doorItem.Items.Add(new OrderItem() { ItemNum = "", UOM = "", Description = comment, LineNum = 0, LineType = "", LotNumber = "", Taxable = "" });
                }
                // Add order type and call EasyWeb to get routing details
                if (detail.ItemMaster != null)
                {
                    foreach (var master in detail.ItemMaster)
                    {
                        //if (master.ITEM_NUM.StartsWith("M")) ewOrder.OrderType = "R"; //remove when order type is set in Input
                        // This code has the order types hard coded.
                        // Will be modified when the order type is set in an input from EasyWeb. 
                        if (master.VAR_4.StartsWith("EA") || master.VAR_4.StartsWith("FT"))
                        {
                            if (master.ITEM_NUM.StartsWith("L")) ewOrder.OrderType = "R"; //remove when order type is set in Input
                            if (master.ITEM_NUM.StartsWith("MILE")) ewOrder.OrderType = "R"; //remove when order type is set in Input
                            if ((master.ITEM_NUM.StartsWith("C") || (master.ITEM_NUM.StartsWith("Lo"))) && (!(master.ITEM_NUM.StartsWith("C2") || (master.ITEM_NUM.StartsWith("C3"))))) ewOrder.OrderType = "C"; //remove when order type is set in Input
                            //ewOrder.OrderType = strOrderType;

                        }
                    }

                    Document document = new Document();
                    document.serialNumber = detail.ID;
                    document.type = "PDF";
                    // document = await conceptAccess.getDocumentAsync(document.);

                    // Get routing configuration data from EasyWeb
                    configuration = new Configuration() { Routing = null };
                  //  if (detail.Routing != null)  //Removed the Routing Not equal to Null, as it was not generating media objects for Raynor Items. e.g. Alumatite generic sections
                 //   {
                        // For residential orders call Easyweb to get routing configuration
                        if (ewOrder.OrderType == "R")
                        {
                            if (detail.SMARTPART_NUM.StartsWith("C-"))
                            {
                                configuration = await conceptAccess.getConfigurationAsync(detail.ID);
                            }
                        }
                        else
                        {
                            // For commercial orders get the routing configuration from Easyweb inputs and the database
                            configuration = new Configuration() { Routing = detail.Routing, Input = new Input[] { } };
                            inputnum = _database.GetTable("select INPUT_NAME, DESIGN_INPUT_VAL, PRODUCT_ID from CO_DES_INPUT where DESIGN_ID='" + detail.ID + "' and INPUT_NAME like '%_%'", "Inputs");
                            if (inputnum > 0)
                            {
                                for (int i = 0; i < inputnum; i++)
                                {
                                    inputitem = _database.DSet.Tables["Inputs"].Rows[i][0].ToString().ToUpper();
                                    inputvalue = _database.DSet.Tables["Inputs"].Rows[i][1].ToString();
                                    if (inputitem == "SPR" && inputvalue != "N" && string.IsNullOrWhiteSpace(doorItem.HoldOrdersCode))
                                    {
                                        //doorItem.HoldOrdersCode = item.Value[0].name;
                                        doorItem.HoldOrdersCode = "E1";
                                    }
                                    if (inputitem == "GL_SPR_1" && inputvalue != "0" && string.IsNullOrWhiteSpace(doorItem.HoldOrdersCode))
                                    {
                                        if (_database.DSet.Tables["Inputs"].Rows[i][2].ToString() == "239222331" || _database.DSet.Tables["Inputs"].Rows[i][2].ToString() == "243841551")
                                        {
                                            doorItem.HoldOrdersCode = "E1";
                                        }
                                        if (_database.DSet.Tables["Inputs"].Rows[i][2].ToString() == "212032411" && (inputvalue == "E1" || inputvalue == "E2"))
                                        {
                                            doorItem.HoldOrdersCode = "E1";
                                        }
                                    }
                                    if (inputitem == "GL_DOOR_MODEL")
                                    {
                                        doorItem.GLDoorModel = inputvalue;
                                    }
                                    if (inputitem == "GL_DOOR_SIZE")
                                    {
                                        doorItem.GLDoorSize = inputvalue;
                                    }
                                    if (inputitem == "GL_NUMBER_OF_SECTION")
                                    {
                                        doorItem.GLNumberOfSection = inputvalue;
                                    }
                                    if (inputitem == "GL_DOOR_COLOUR")
                                    {
                                        doorItem.GLDoorColour = inputvalue;
                                    }
                                    if (inputitem == "GL_STYLE")
                                    {
                                        doorItem.GLStyle = inputvalue;
                                    }
                                    if (inputitem == "GL_END_CAPS")
                                    {
                                        doorItem.GLEndCaps = inputvalue;
                                    }
                                    if (inputitem == "GL_LIFT_TYPE")
                                    {
                                        doorItem.GLLiftType = inputvalue;
                                    }
                                    if (inputitem == "Gl_HL_AMT")
                                    {
                                        doorItem.GLHlAmt = inputvalue;
                                    }
                                    if (inputitem == "GL_TOP_WEATHER_SEAL")
                                    {
                                        doorItem.GLTopWeatherSeal = inputvalue;
                                    }
                                    if (inputitem == "GL_BOTTOM_SEAL")
                                    {
                                        doorItem.GLBottomSeal = inputvalue;
                                    }
                                    if (inputitem == "GL_TRUSS_STYLE")
                                    {
                                        doorItem.GLTrussStyle = inputvalue;
                                    }
                                    if (inputitem == "GL_GLAZING_TYPE")
                                    {
                                        doorItem.GLGlazingType = inputvalue;
                                    }
                                    if (inputitem == "GL_ALUM_GLAZING_NOTE ")
                                    {
                                        doorItem.GlALumGlazingType = inputvalue;
                                    }
                                    if (inputitem == "GL_WINDOW_TYPE")
                                    {
                                        doorItem.GLWindowType = inputvalue;
                                    }
                                    if (inputitem == "GL_GLASS_TYPE")
                                    {
                                        doorItem.GLGlassType = inputvalue;
                                    }
                                    if (inputitem == "GL_FRAME_COLOUR")
                                    {
                                        doorItem.GLFrameColour = inputvalue;
                                    }
                                    if (inputitem == "GL_LITES_PER_SPACING")
                                    {
                                        doorItem.GLLitesPerSpacing = inputvalue;
                                    }
                                    if (inputitem == "GL_SPACING")
                                    {
                                        doorItem.GLSpacing = inputvalue;
                                    }
                                    if (inputitem == "GL_HARDWARE_SIZE")
                                    {
                                        doorItem.GLHardwareSize = inputvalue;
                                    }
                                    if (inputitem == "GL_MOUNT_TYPE")
                                    {
                                        doorItem.GLMountType = inputvalue;
                                    }
                                    if (inputitem == "GL_JAMB")
                                    {
                                        doorItem.GLJamb = inputvalue;
                                    }
                                    if (inputitem == "GL_SHAFT_TYPE")
                                    {
                                        doorItem.GLShaftType = inputvalue;
                                    }
                                    if (inputitem == "GL_SPRING_RH")
                                    {
                                        doorItem.GLSpringRH = inputvalue;
                                    }
                                    if (inputitem == "GL_SPRING_LH")
                                    {
                                        doorItem.GLSpringLH = inputvalue;
                                    }
                                    if (inputitem == "GL_SPRING_RH_DESCRIPTION")
                                    {
                                        doorItem.GLSpringRhDesc = inputvalue;
                                    }
                                    if (inputitem == "GL_SPRING_LH_DESCRIPTION")
                                    {
                                        doorItem.GLSpringLhDesc = inputvalue;
                                    }
                                    if (inputitem == "GL_EXTENSION_SPRING")
                                    {
                                        doorItem.GLExtensionSpring = inputvalue;
                                    }                                
                                    if (inputitem == "GL_MEDIA_ATTACHMENT_SB")
                                    {
                                        doorItem.GLMediaAttachmentSB = inputvalue;
                                    }
                                    if (inputitem == "MA_SB_BTM")
                                    {
                                        doorItem.MA_SB_BTM = inputvalue;
                                    }
                                    if (inputitem == "MA_SB_GLZ")
                                    {
                                        doorItem.MA_SB_GLZ = inputvalue;
                                    }
                                    if (inputitem == "MA_SB_INT_1")
                                    {
                                        doorItem.MA_SB_INT_1 = inputvalue;
                                    }
                                    if (inputitem == "MA_SB_INT_2")
                                    {
                                        doorItem.MA_SB_INT_2 = inputvalue;
                                    }
                                    if (inputitem ==  "ORDER_TYPE")
                                    {
                                        ewOrder.OrderType = inputvalue;
                                    }
                                    if (inputitem == "SPR")
                                    {
                                        ewOrder.SPR = inputvalue;
                                    }
                                    if(inputitem == "SPR_DETAIL")
                                    {
                                        ewOrder.SPR_DETAIL = inputvalue;
                                    }
                                    if (inputitem == "TAG")
                                    {
                                        ewOrder.JobTag = inputvalue;
                                    }
                                    if(inputitem == "GL_INVERTED_CURTAIN")
                                    {
                                        doorItem.GLInvertedCurtain = inputvalue;
                                    }
                                    if(inputitem == "GL_JAMB_TYPE")
                                    {
                                        doorItem.GLJambType = inputvalue;
                                    }
                                    if(inputitem == "GL_SLATS")
                                    {
                                        doorItem.GLSlates = inputvalue;
                                    }
                                    if(inputitem == "GL_GUIDES")
                                    {
                                        doorItem.GlGuides = inputvalue;
                                    }
                                    if(inputitem == "GL_EL_WL")
                                    {
                                        doorItem.GLElWl = inputvalue;
                                    }
                                    if(inputitem == "GL_DRIVE")
                                    {
                                        doorItem.GLDrive = inputvalue;
                                    }
                                    if(inputitem == "GL_CURTAIN_RAL")
                                    {
                                        doorItem.GLCurtainRal = inputvalue;
                                    }
                                    if(inputitem == "GL_HOOD_RAL")
                                    {
                                        doorItem.GLHoodRal = inputvalue;
                                    }
                                    if (inputitem == "GL_GUIDES_RAL")
                                    {
                                        doorItem.GLGuidesRal = inputvalue;
                                    }
                                    if (inputitem == "GL_FASCIA_RAL")
                                    {
                                        doorItem.GLFasciaRal = inputvalue;
                                    }
                                    if (inputitem == "GL_BOTTOM_BAR_RAL")
                                    {
                                        doorItem.GLBottomBarRal = inputvalue;
                                    }
                                    if (inputitem == "GL_JAMB_GUIDE_WEATHERSEAL")
                                    {
                                        doorItem.GLJambGuideWS = inputvalue;
                                    }
                                    if (inputitem == "GL_HEADER_SEAL")
                                    {
                                        doorItem.GLHeaderSeal = inputvalue;
                                    }
                                    if (inputitem == "GL_LITES_TYPE")
                                    {
                                        doorItem.GLLitesType = inputvalue;
                                    }
                                    if (inputitem == "GL_LOCKS")
                                    {
                                        doorItem.GLLocks = inputvalue;
                                    }
                                    if (inputitem == "GL_SLOPED_BOTTOM_BAR")
                                    {
                                        doorItem.GLSlopedBottomBar = inputvalue;
                                    }
                                    if (inputitem == "GL_HOOD")
                                    {
                                        doorItem.GLHood = inputvalue;
                                    }
                                    if (inputitem == "GL_MASONRY_CLIP")
                                    {
                                        doorItem.GLMasonryClip = inputvalue;
                                    }
                                    if (inputitem == "GL_MOUNTING_PLATES")
                                    {
                                        doorItem.GLMountingPlates = inputvalue;
                                    }
                                    if (inputitem == "GL_SUPPORT_BRACKETS")
                                    {
                                        doorItem.GlSupportBrackets = inputvalue;
                                    }
                                    if (inputitem == "GL_PERFORATED_SLATS")
                                    {
                                        doorItem.GLPerforatedSlats = inputvalue;
                                    }
                                    if (inputitem == "GL_BOTTOM_BAR")
                                    {
                                        doorItem.GlBottomBar = inputvalue;
                                    }
                                    if(inputitem == "GL_HOUR_RATING")
                                    {
                                    doorItem.GLHourRating = inputvalue;
                                    }
                                    if (inputitem == "GL_SPRING_CYCLE_LIFE ")
                                    {
                                        doorItem.GLSpringCycleLife = inputvalue;
                                    }
                                    if (inputitem == "GL_FUSIBLE_LINK")
                                    {
                                        doorItem.GLFusibleLink = inputvalue;
                                    }
                                    if(inputitem == "SEC_1_SEC_BDL_RP")
                                    {
                                        doorItem.SEC_1_SEC_BDL_RP = inputvalue;
                                    }                                  
                                    if (inputitem == "SEC_2_SEC_BDL_RP")
                                    {
                                        doorItem.SEC_2_SEC_BDL_RP = inputvalue;
                                    }
                                    if (inputitem == "SEC_3_SEC_BDL_RP")
                                    {
                                        doorItem.SEC_3_SEC_BDL_RP = inputvalue;
                                    }
                                    if (inputitem == "SEC_4_SEC_BDL_RP")
                                    {
                                        doorItem.SEC_4_SEC_BDL_RP = inputvalue;
                                    }
                                    if (inputitem == "SEC_5_SEC_BDL_RP")
                                    {
                                        doorItem.SEC_5_SEC_BDL_RP = inputvalue;
                                    }
                                    if (inputitem == "SEC_6_SEC_BDL_RP")
                                    {
                                        doorItem.SEC_6_SEC_BDL_RP = inputvalue;
                                    }
                                    if (inputitem == "SEC_7_SEC_BDL_RP")
                                    {
                                        doorItem.SEC_7_SEC_BDL_RP = inputvalue;
                                    }
                                    if (inputitem == "SEC_8_SEC_BDL_RP")
                                    {
                                        doorItem.SEC_8_SEC_BDL_RP = inputvalue;
                                    }
                                    if (inputitem == "SEC_9_SEC_BDL_RP")
                                    {
                                        doorItem.SEC_9_SEC_BDL_RP = inputvalue;
                                    }
                                    if (inputitem == "SEC_10_SEC_BDL_RP")
                                    {
                                        doorItem.SEC_10_SEC_BDL_RP = inputvalue;
                                    }
                                    if (inputitem == "SEC_BTM_SEC_BDL_RP")
                                    {
                                        doorItem.SEC_BTM_SEC_BDL_RP = inputvalue;
                                    }
                                    if (inputitem == "SEC_1")
                                    {
                                        doorItem.SEC_1 = inputvalue;
                                    }
                                    if (inputitem == "SEC_2")
                                    {
                                        doorItem.SEC_2 = inputvalue;
                                    }
                                    if (inputitem == "SEC_3")
                                    {
                                        doorItem.SEC_3 = inputvalue;
                                    }
                                    if (inputitem == "SEC_4")
                                    {
                                        doorItem.SEC_4 = inputvalue;
                                    }
                                    if (inputitem == "SEC_5")
                                    {
                                        doorItem.SEC_5 = inputvalue;
                                    }
                                    if (inputitem == "SEC_6")
                                    {
                                        doorItem.SEC_6 = inputvalue;
                                    }
                                    if (inputitem == "SEC_7")
                                    {
                                        doorItem.SEC_7 = inputvalue;
                                    }
                                    if (inputitem == "SEC_8")
                                    {
                                        doorItem.SEC_8 = inputvalue;
                                    }
                                    if (inputitem == "SEC_9")
                                    {
                                        doorItem.SEC_9 = inputvalue;
                                    }
                                    if (inputitem == "SEC_10")
                                    {
                                        doorItem.SEC_10 = inputvalue;
                                    }
                                    if (inputitem == "SEC_BTM")
                                    {
                                        doorItem.SEC_BTM = inputvalue;
                                    }
                                    if (inputitem == "SEC_1_PANEL_QTY")
                                    {
                                        doorItem.SEC_1_PANEL_QTY = inputvalue;
                                    }
                                    if (inputitem == "SEC_2_PANEL_QTY")
                                    {
                                        doorItem.SEC_2_PANEL_QTY = inputvalue;
                                    }
                                    if (inputitem == "SEC_3_PANEL_QTY")
                                    {
                                        doorItem.SEC_3_PANEL_QTY = inputvalue;
                                    }
                                    if (inputitem == "SEC_4_PANEL_QTY")
                                    {
                                        doorItem.SEC_4_PANEL_QTY = inputvalue;
                                    }
                                    if (inputitem == "SEC_5_PANEL_QTY")
                                    {
                                        doorItem.SEC_5_PANEL_QTY = inputvalue;
                                    }
                                    if (inputitem == "SEC_6_PANEL_QTY")
                                    {
                                        doorItem.SEC_6_PANEL_QTY = inputvalue;
                                    }
                                    if (inputitem == "SEC_7_PANEL_QTY")
                                    {
                                        doorItem.SEC_7_PANEL_QTY = inputvalue;
                                    }
                                    if (inputitem == "SEC_8_PANEL_QTY")
                                    {
                                        doorItem.SEC_8_PANEL_QTY = inputvalue;
                                    }
                                    if (inputitem == "SEC_9_PANEL_QTY")
                                    {
                                        doorItem.SEC_9_PANEL_QTY = inputvalue;
                                    }
                                    if (inputitem == "SEC_10_PANEL_QTY")
                                    {
                                        doorItem.SEC_10_PANEL_QTY = inputvalue;
                                    }
                                    if (inputitem == "SEC_BTM_PANEL_QTY")
                                    {
                                        doorItem.SEC_BTM_PANEL_QTY = inputvalue;
                                    }
                                    if (inputitem == "PANEL_CONFIGURATION_SEC_1")
                                    {
                                        doorItem.PANEL_CONFIGURATION_SEC_1 = inputvalue;
                                    }
                                    if (inputitem == "PANEL_CONFIGURATION_SEC_2")
                                    {
                                        doorItem.PANEL_CONFIGURATION_SEC_2 = inputvalue;
                                    }
                                    if (inputitem == "PANEL_CONFIGURATION_SEC_3")
                                    {
                                        doorItem.PANEL_CONFIGURATION_SEC_3 = inputvalue;
                                    }
                                    if (inputitem == "PANEL_CONFIGURATION_SEC_4")
                                    {
                                        doorItem.PANEL_CONFIGURATION_SEC_4 = inputvalue;
                                    }
                                    if (inputitem == "PANEL_CONFIGURATION_SEC_5")
                                    {
                                        doorItem.PANEL_CONFIGURATION_SEC_5 = inputvalue;
                                    }
                                    if (inputitem == "PANEL_CONFIGURATION_SEC_6")
                                    {
                                        doorItem.PANEL_CONFIGURATION_SEC_6 = inputvalue;
                                    }
                                    if (inputitem == "PANEL_CONFIGURATION_SEC_7")
                                    {
                                        doorItem.PANEL_CONFIGURATION_SEC_7 = inputvalue;
                                    }
                                    if (inputitem == "PANEL_CONFIGURATION_SEC_8")
                                    {
                                        doorItem.PANEL_CONFIGURATION_SEC_8 = inputvalue;
                                    }
                                    if (inputitem == "PANEL_CONFIGURATION_SEC_9")
                                    {
                                        doorItem.PANEL_CONFIGURATION_SEC_9 = inputvalue;
                                    }
                                    if (inputitem == "PANEL_CONFIGURATION_SEC_10")
                                    {
                                        doorItem.PANEL_CONFIGURATION_SEC_10 = inputvalue;
                                    }
                                    if (inputitem == "PANEL_CONFIGURATION_SEC_BTM")
                                    {
                                        doorItem.PANEL_CONFIGURATION_SEC_BTM = inputvalue;
                                    }
                                    if (inputitem == "GL_ALUM_BOTTOM_SECTION_TYPE ")
                                    {
                                        doorItem.GL_ALUM_BTM_SEC_TYPE = inputvalue;
                                    }

                            }
                            }
                        }
                   // }
                }

                // Check for exclude of glazing items in order detail
                bool input1 = false;
                bool input2 = false;
                bool input3 = false; // Not used
                // Get all required configuration inputs
                if (configuration.Input != null)
                {
                    foreach (var item in configuration.Input)
                    {
                        if (item != null)
                        {
                            item.name = item.name.ToUpper();
                            if (item.name == "DOOR_REINFORCING_STRIP" && item.Value[0].name == "N")
                            {
                                input1 = true;
                                ewOrder.CreatePO = true;
                            }
                            if (item.name == "WINDOWS_YESNO" && item.Value[0].name == "YES")
                            {
                                input2 = true;
                            }
                            if (item.name == "INSERT_GRID_STYLE" && (item.Value[0].name == "DESIGN_G" || item.Value[0].name == "DESIGN_B"))
                            {
                                input3 = true;
                            }
                            if (item.name == "SPR" && item.Value[0].name != "N" && string.IsNullOrWhiteSpace(doorItem.HoldOrdersCode))
                            {
                                //doorItem.HoldOrdersCode = item.Value[0].name;
                                doorItem.HoldOrdersCode = "E1";
                            }
                            if (item.name == "GL_SPR_1" && item.Value[0].name != "0" && string.IsNullOrWhiteSpace(doorItem.HoldOrdersCode))
                            {
                                inputnum = _database.GetTable("select INPUT_NAME, DESIGN_INPUT_VAL, PRODUCT_ID from CO_DES_INPUT where DESIGN_ID='" + detail.ID + "' and INPUT_NAME like 'GL_%_%'", "Inputs");
                                if (_database.DSet.Tables["Inputs"].Rows.Count > 0)
                                {
                                    if (_database.DSet.Tables["Inputs"].Rows[0][2].ToString() == "239222331" || _database.DSet.Tables["Inputs"].Rows[0][2].ToString() == "243841551")
                                    {
                                        doorItem.HoldOrdersCode = "E1";
                                    }
                                    if (_database.DSet.Tables["Inputs"].Rows[0][2].ToString() == "212032411" && (item.Value[0].name == "E1" || item.Value[0].name == "E2"))
                                    {
                                        doorItem.HoldOrdersCode = "E1";
                                    }
                                }
                            }
                            if (item.name == "GL_DOOR_MODEL")
                            {
                                doorItem.GLDoorModel = item.Value[0].label;
                            }
                            /*
                            if (item.name == "GL_ORDER_STATUS1")
                            {
                                doorItem.GLOrderStatus = item.Value[0].name;
                            }

                            if (item.name == "GL_REASON_CODES1")
                            {
                                doorItem.GLReasonCodes = item.Value[0].name;
                            }

                            */
                            if (item.name == "GL_DOOR_SIZE")
                            {
                                doorItem.GLDoorSize = item.Value[0].name;
                            }
                            if (item.name == "GL_NUMBER_OF_SECTION")
                            {
                                doorItem.GLNumberOfSection = item.Value[0].name;
                            }
                            if (item.name == "GL_DOOR_COLOUR")
                            {
                                doorItem.GLDoorColour = item.Value[0].label;
                            }
                            if (item.name == "GL_STYLE")
                            {
                                doorItem.GLStyle = item.Value[0].name;
                            }
                            if (item.name == "GL_END_CAPS")
                            {
                                doorItem.GLEndCaps = item.Value[0].name;
                            }
                            if (item.name == "GL_LIFT_TYPE")
                            {
                                doorItem.GLLiftType = item.Value[0].name;
                            }
                            if (item.name == "GL_HL_AMT")
                            {
                                doorItem.GLHlAmt = item.Value[0].name;
                            }
                            if (item.name == "GL_TOP_WEATHER_SEAL")
                            {
                                doorItem.GLTopWeatherSeal = item.Value[0].name;
                            }
                            if (item.name == "GL_BOTTOM_SEAL")
                            {
                                doorItem.GLBottomSeal = item.Value[0].name;
                            }
                            if (item.name == "GL_TRUSS_STYLE")
                            {
                                doorItem.GLTrussStyle = item.Value[0].name;
                            }
                            if (item.name == "GL_GLAZING_TYPE")
                            {
                                doorItem.GLGlazingType = item.Value[0].name;
                            }
                            if (item.name == "GL_ALUM_GLAZING_NOTE ")
                            {
                                doorItem.GlALumGlazingType = item.Value[0].name;
                            }
                            if (item.name == "GL_WINDOW_TYPE")
                            {
                                doorItem.GLWindowType = item.Value[0].name;
                            }
                            if (item.name == "GL_GLASS_TYPE")
                            {
                                doorItem.GLGlassType = item.Value[0].name;
                            }
                            if (item.name == "GL_FRAME_COLOUR")
                            {
                                doorItem.GLFrameColour = item.Value[0].name;
                            }
                            if (item.name == "GL_LITES_PER_SPACING")
                            {
                                doorItem.GLLitesPerSpacing = item.Value[0].name;
                            }
                            if (item.name == "GL_SPACING")
                            {
                                doorItem.GLSpacing = item.Value[0].name;
                            }
                            if (item.name == "GL_HARDWARE_SIZE")
                            {
                                doorItem.GLHardwareSize = item.Value[0].name;
                            }
                            if (item.name == "GL_MOUNT_TYPE")
                            {
                                doorItem.GLMountType = item.Value[0].name;
                            }
                            if (item.name == "GL_JAMB")
                            {
                                doorItem.GLJamb = item.Value[0].name;
                            }
                        if (item.name == "GL_SHAFT_TYPE")
                            {
                                doorItem.GLShaftType = item.Value[0].name;
                            }
                            if (item.name == "GL_SPRING_RH")
                            {
                                doorItem.GLSpringRH = item.Value[0].name;
                            }
                            if (item.name == "GL_SPRING_LH")
                            {
                                doorItem.GLSpringLH = item.Value[0].name;
                            }
                            if (item.name == "GL_SPRING_RH_DESCRIPTION")
                            {
                                doorItem.GLSpringRhDesc = item.Value[0].name;
                            }
                            if (item.name == "GL_SPRING_LH_DESCRIPTION")
                            {
                                doorItem.GLSpringLhDesc = item.Value[0].name;
                            }
                            if (item.name == "GL_EXTENSION_SPRING")
                            {
                                doorItem.GLExtensionSpring = item.Value[0].name;
                            }
                            if (item.name == "GL_MEDIA_ATTACHMENT_SB")
                            {
                                doorItem.GLMediaAttachmentSB = item.Value[0].name;
                            }
                            if (item.name == "MA_SB_BTM")
                            {
                                doorItem.MA_SB_BTM = item.Value[0].name;
                            }
                            if (item.name == "MA_SB_GLZ")
                            {
                                doorItem.MA_SB_GLZ = item.Value[0].name;
                            }
                            if (item.name == "MA_SB_INT_1")
                            {
                                doorItem.MA_SB_INT_1 = item.Value[0].name;
                            }
                            if (item.name == "MA_SB_INT_2")
                            {
                                doorItem.MA_SB_INT_2 = item.Value[0].name;
                            }
                            if(item.name == "GLZ_CODE_SB_RP_1")
                            {
                                doorItem.GLZ_CODE_SB_RP_1 = item.Value[0].name;
                            }                           
                            if(item.name == "GLZ_CODE_SB_RP_2")
                            {
                                doorItem.GLZ_CODE_SB_RP_2 = item.Value[0].name;
                            }
                            if(item.name == "GLZ_CODE_INT_1_RP_1")
                            {
                                doorItem.GLZ_CODE_INT_1_RP_1 = item.Value[0].name;
                            }
                            if(item.name == "GLZ_CODE_INT_1_RP_2")
                            {
                                doorItem.GLZ_CODE_INT_1_RP_2 = item.Value[0].name;
                            }
                            if(item.name == "GLZ_CODE_INT_2_RP_1")
                            {
                                doorItem.GLZ_CODE_INT_2_RP_1 = item.Value[0].name;
                            }
                            if(item.name == "GLZ_CODE_INT_2_RP_2")
                            {
                                doorItem.GLZ_CODE_INT_2_RP_2 = item.Value[0].name;
                            }
                            if(item.name == "GLZ_CODE_INT_3_RP_1")
                            {
                                doorItem.GLZ_CODE_INT_3_RP_1 = item.Value[0].name; 
                            }
                            if(item.name == "GLZ_CODE_INT_3_RP_2")
                            {
                                doorItem.GLZ_CODE_INT_3_RP_2 = item.Value[0].name;
                            }
                            if (item.name == "ORDER_TYPE")
                            {
                                ewOrder.OrderType = item.Value[0].name;
                            }
                            if (item.name == "SPR")
                            {
                                ewOrder.SPR = item.Value[0].name;
                            }
                            if(item.name == "SPR_DETAIL")
                            {
                                ewOrder.SPR_DETAIL = item.Value[0].name;
                            }
                            if (item.name == "TAG")
                            {
                                ewOrder.JobTag = item.Value[0].name;
                            }
                            if(item.name == "CNC_SB_RP1")
                            {
                                doorItem.CNC_SB_RP1 = item.Value[0].name;
                            }
                            if (item.name == "CNC_SB_RP2")
                            {
                                doorItem.CNC_SB_RP2 = item.Value[0].name;
                            }
                            
                            if (item.name == "CNC_INT1_RP1")
                            {
                                doorItem.CNC_INT1_RP1 = item.Value[0].name;
                            }
                            if (item.name == "CNC_INT1_RP2")
                            {
                                doorItem.CNC_INT1_RP2 = item.Value[0].name;
                            }
                            if (item.name == "CNC_INT2_RP1")
                            {
                                doorItem.CNC_INT2_RP1 = item.Value[0].name;
                            }
                            if (item.name == "CNC_INT2_RP2")
                            {
                                doorItem.CNC_INT2_RP2 = item.Value[0].name;
                            }
                            if (item.name == "CNC_INT3_RP1")
                            {
                                doorItem.CNC_INT3_RP2 = item.Value[0].name;
                            }
                            if (item.name == "CNC_INT3_RP2")
                            {
                                doorItem.CNC_INT3_RP2 = item.Value[0].name;
                            }
                            if (item.name == "PANEL_CODE")
                            {
                                doorItem.PANEL_CODE = item.Value[0].name;
                            }
                            if (item.name == "PC_SB_RP1")
                            {
                                doorItem.PC_SB_RP1 = item.Value[0].name;
                            }
                            if (item.name == "PC_SB_RP2")
                            {
                                doorItem.PC_SB_RP2 = item.Value[0].name;
                            }
                            if (item.name == "PC_INT1_RP1")
                            {
                                doorItem.PC_INT1_RP1 = item.Value[0].name;
                            }
                            if (item.name == "PC_INT1_RP2")
                            {
                                doorItem.PC_INT1_RP2 = item.Value[0].name;
                            }
                            if (item.name == "PC_INT2_RP1")
                            {
                                doorItem.PC_INT2_RP1 = item.Value[0].name;
                            }
                            if (item.name == "PC_INT2_RP2")
                            {
                                doorItem.PC_INT2_RP2 = item.Value[0].name;
                            }
                            if (item.name == "PC_INT3_RP1")
                            {
                                doorItem.PC_INT3_RP1 = item.Value[0].name;
                            }
                            if (item.name == "PC_INT3_RP2")
                            {
                                doorItem.PC_INT3_RP2 = item.Value[0].name;
                            }
                            if (item.name == "SB_RP1_PANEL_TYPE")
                            {
                                doorItem.SB_RP_1_PANEL_TYPE = item.Value[0].name;
                            }
                            if(item.name == "SB_RP2_PANEL_TYPE")
                            {
                                doorItem.SB_RP_2_PANEL_TYPE = item.Value[0].name;
                            }
                            if(item.name == "INT1_RP1_PANEL_TYPE")
                            {
                                doorItem.INT1_RP1_PANEL_TYPE = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP1_PANEL_TYPE")
                            {
                                doorItem.INT1_RP1_PANEL_TYPE = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP2_PANEL_TYPE")
                            {
                                doorItem.INT1_RP2_PANEL_TYPE = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP1_PANEL_TYPE")
                            {
                                doorItem.INT2_RP1_PANEL_TYPE = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP2_PANEL_TYPE")
                            {
                                doorItem.INT2_RP2_PANEL_TYPE = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP1_PANEL_TYPE")
                            {
                                doorItem.INT3_RP1_PANEL_TYPE = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP2_PANEL_TYPE")
                            {
                                doorItem.INT3_RP2_PANEL_TYPE = item.Value[0].name;
                            }
                            if(item.name == "SB_RP_1_ORPHAN")
                            {
                                doorItem.SB_RP_1_ORPHAN = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_2_ORPHAN")
                            {
                                doorItem.SB_RP_2_ORPHAN = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_1_ORPHAN")
                            {
                                doorItem.INT1_RP_1_ORPHAN = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_2_ORPHAN")
                            {
                                doorItem.INT1_RP_2_ORPHAN = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_1_ORPHAN")
                            {
                                doorItem.INT2_RP_1_ORPHAN = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_2_ORPHAN")
                            {
                                doorItem.INT2_RP_2_ORPHAN = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_1_ORPHAN")
                            {
                                doorItem.INT3_RP_1_ORPHAN = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_2_ORPHAN")
                            {
                                doorItem.INT3_RP_2_ORPHAN = item.Value[0].name;
                            }
                            if (item.name == "SB_RP1_PANEL_CONFIGURATION")
                            {
                                doorItem.SB_RP1_PANEL_CONFIGURATION = item.Value[0].name;
                            }
                            if (item.name == "SB_RP2_PANEL_CONFIGURATION")
                            {
                                doorItem.SB_RP2_PANEL_CONFIGURATION = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP1_PANEL_CONFIGURATION")
                            {
                                doorItem.INT1_RP1_PANEL_CONFIGURATION = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP2_PANEL_CONFIGURATION")
                            {
                                doorItem.INT1_RP2_PANEL_CONFIGURATION = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP1_PANEL_CONFIGURATION")
                            {
                                doorItem.INT2_RP1_PANEL_CONFIGURATION = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP2_PANEL_CONFIGURATION")
                            {
                                doorItem.INT2_RP2_PANEL_CONFIGURATION = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP1_PANEL_CONFIGURATION")
                            {
                                doorItem.INT3_RP1_PANEL_CONFIGURATION = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP2_PANEL_CONFIGURATION")
                            {
                                doorItem.INT3_RP2_PANEL_CONFIGURATION = item.Value[0].name;
                            }
                            if (item.name == "GL_ALUM_BOTTOM_SECTION_TYPE ")
                            {
                                doorItem.GL_ALUM_BTM_SEC_TYPE = item.Value[0].name;
                            }
                            if(item.name == "SB_RP_1_DOOR_MODEL")
                            {
                                doorItem.SB_RP_1_DOOR_MODEL = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_2_DOOR_MODEL")
                            {
                                doorItem.SB_RP_2_DOOR_MODEL = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_1_DOOR_MODEL")
                            {
                                doorItem.INT1_RP_1_DOOR_MODEL = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_2_DOOR_MODEL")
                            {
                                doorItem.INT1_RP_2_DOOR_MODEL = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_1_DOOR_MODEL")
                            {
                                doorItem.INT2_RP_1_DOOR_MODEL = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_2_DOOR_MODEL")
                            {
                                doorItem.INT2_RP_2_DOOR_MODEL = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_1_DOOR_MODEL")
                            {
                                doorItem.INT3_RP_1_DOOR_MODEL = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_2_DOOR_MODEL")
                            {
                                doorItem.INT3_RP_2_DOOR_MODEL = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_1_PANEL_STYLE")
                            {
                                doorItem.SB_RP_1_PANEL_STYLE = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_2_PANEL_STYLE")
                            {
                                doorItem.SB_RP_2_PANEL_STYLE = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_1_PANEL_STYLE")
                            {
                                doorItem.INT1_RP_1_PANEL_STYLE = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_2_PANEL_STYLE")
                            {
                                doorItem.INT1_RP_2_PANEL_STYLE = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_1_PANEL_STYLE")
                            {
                                doorItem.INT2_RP_1_PANEL_STYLE = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_2_PANEL_STYLE")
                            {
                                doorItem.INT2_RP_2_PANEL_STYLE = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_1_PANEL_STYLE")
                            {
                                doorItem.INT3_RP_1_PANEL_STYLE = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_2_PANEL_STYLE")
                            {
                                doorItem.INT3_RP_2_PANEL_STYLE = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_1_DOOR_COLOUR")
                            {
                                doorItem.SB_RP_1_DOOR_COLOUR = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_2_DOOR_COLOUR")
                            {
                                doorItem.SB_RP_2_DOOR_COLOUR = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_1_DOOR_COLOUR")
                            {
                                doorItem.INT1_RP_1_DOOR_COLOUR = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_2_DOOR_COLOUR")
                            {
                                doorItem.INT1_RP_2_DOOR_COLOUR = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_1_DOOR_COLOUR")
                            {
                                doorItem.INT2_RP_1_DOOR_COLOUR = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_2_DOOR_COLOUR")
                            {
                                doorItem.INT2_RP_2_DOOR_COLOUR = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_1_DOOR_COLOUR")
                            {
                                doorItem.INT3_RP_1_DOOR_COLOUR = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_2_DOOR_COLOUR")
                            {
                                doorItem.INT3_RP_2_DOOR_COLOUR = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_1_DRILL_FOR_HINGES")
                            {
                                doorItem.SB_RP_1_DRILL_FOR_HINGES = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_2_DRILL_FOR_HINGES")
                            {
                                doorItem.SB_RP_2_DRILL_FOR_HINGES = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_1_DRILL_FOR_HINGES")
                            {
                                doorItem.INT1_RP_1_DRILL_FOR_HINGES = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_2_DRILL_FOR_HINGES")
                            {
                                doorItem.INT1_RP_2_DRILL_FOR_HINGES = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_1_DRILL_FOR_HINGES")
                            {
                                doorItem.INT2_RP_1_DRILL_FOR_HINGES = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_2_DRILL_FOR_HINGES")
                            {
                                doorItem.INT2_RP_2_DRILL_FOR_HINGES = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_1_DRILL_FOR_HINGES")
                            {
                                doorItem.INT3_RP_1_DRILL_FOR_HINGES = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_2_DRILL_FOR_HINGES")
                            {
                                doorItem.INT3_RP_2_DRILL_FOR_HINGES = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_1_DRILL_CODE")
                            {
                                doorItem.SB_RP_1_DRILL_CODE = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_2_DRILL_CODE")
                            {
                                doorItem.SB_RP_2_DRILL_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_1_DRILL_CODE")
                            {
                                doorItem.INT1_RP_1_DRILL_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_2_DRILL_CODE")
                            {
                                doorItem.INT1_RP_2_DRILL_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_1_DRILL_CODE")
                            {
                                doorItem.INT2_RP_1_DRILL_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_2_DRILL_CODE")
                            {
                                doorItem.INT2_RP_2_DRILL_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_1_DRILL_CODE")
                            {
                                doorItem.INT3_RP_1_DRILL_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_1_DRILL_CODE")
                            {
                                doorItem.INT3_RP_1_DRILL_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_2_DRILL_CODE")
                            {
                                doorItem.INT3_RP_2_DRILL_CODE = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_1_GLAZED")
                            {
                                doorItem.SB_RP_1_GLAZED = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_2_GLAZED")
                            {
                                doorItem.SB_RP_2_GLAZED = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_1_GLAZED")
                            {
                                doorItem.INT1_RP_1_GLAZED = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_2_GLAZED")
                            {
                                doorItem.INT1_RP_2_GLAZED = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_1_GLAZED")
                            {
                                doorItem.INT2_RP_1_GLAZED = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_2_GLAZED")
                            {
                                doorItem.INT2_RP_2_GLAZED = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_1_GLAZED")
                            {
                                doorItem.INT3_RP_1_GLAZED = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_2_GLAZED")
                            {
                                doorItem.INT3_RP_2_GLAZED = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_1_BOTTOM_RTNR_SEAL")
                            {
                                doorItem.SB_RP_1_BOTTOM_RTNR_SEAL = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_2_BOTTOM_RTNR_SEAL")
                            {
                                doorItem.SB_RP_2_BOTTOM_RTNR_SEAL = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_1_BOTTOM_RTNR_SEAL")
                            {
                                doorItem.INT1_RP_1_BOTTOM_RTNR_SEAL = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_2_BOTTOM_RTNR_SEAL")
                            {
                                doorItem.INT1_RP_2_BOTTOM_RTNR_SEAL = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_1_BOTTOM_RTNR_SEAL")
                            {
                                doorItem.INT2_RP_1_BOTTOM_RTNR_SEAL = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_2_BOTTOM_RTNR_SEAL")
                            {
                                doorItem.INT2_RP_2_BOTTOM_RTNR_SEAL = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_1_BOTTOM_RTNR_SEAL")
                            {
                                doorItem.INT3_RP_1_BOTTOM_RTNR_SEAL = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_2_BOTTOM_RTNR_SEAL")
                            {
                                doorItem.INT3_RP_2_BOTTOM_RTNR_SEAL = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_1_END_CAP")
                            {
                                doorItem.SB_RP_1_END_CAP = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_2_END_CAP")
                            {
                                doorItem.SB_RP_2_END_CAP = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_1_END_CAP")
                            {
                                doorItem.INT1_RP_1_END_CAP = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_2_END_CAP")
                            {
                                doorItem.INT1_RP_2_END_CAP = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_1_END_CAP")
                            {
                                doorItem.INT2_RP_1_END_CAP = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_2_END_CAP")
                            {
                                doorItem.INT2_RP_2_END_CAP = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_1_END_CAP")
                            {
                                doorItem.INT3_RP_1_END_CAP = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_2_END_CAP")
                            {
                                doorItem.INT3_RP_2_END_CAP = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_1_PANEL_SEQUENCE")
                            {
                                doorItem.SB_RP_1_PANEL_SEQUENCE = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_2_PANEL_SEQUENCE")
                            {
                                doorItem.SB_RP_2_PANEL_SEQUENCE = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_1_PANEL_SEQUENCE")
                            {
                                doorItem.INT1_RP_1_PANEL_SEQUENCE = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_2_PANEL_SEQUENCE")
                            {
                                doorItem.INT1_RP_2_PANEL_SEQUENCE = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_1_PANEL_SEQUENCE")
                            {
                                doorItem.INT2_RP_1_PANEL_SEQUENCE = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_2_PANEL_SEQUENCE")
                            {
                                doorItem.INT2_RP_2_PANEL_SEQUENCE = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_1_PANEL_SEQUENCE")
                            {
                                doorItem.INT3_RP_1_PANEL_SEQUENCE = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_2_PANEL_SEQUENCE")
                            {
                                doorItem.INT3_RP_2_PANEL_SEQUENCE = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_1_SMART_COM_CODE")
                            {
                                doorItem.SB_RP_1_SMART_COM_CODE = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_2_SMART_COM_CODE")
                            {
                                doorItem.SB_RP_2_SMART_COM_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_1_SMART_COM_CODE")
                            {
                                doorItem.INT1_RP_1_SMART_COM_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_2_SMART_COM_CODE")
                            {
                                doorItem.INT1_RP_2_SMART_COM_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_1_SMART_COM_CODE")
                            {
                                doorItem.INT2_RP_1_SMART_COM_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_2_SMART_COM_CODE")
                            {
                                doorItem.INT2_RP_2_SMART_COM_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_1_SMART_COM_CODE")
                            {
                                doorItem.INT3_RP_1_SMART_COM_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_2_SMART_COM_CODE")
                            {
                                doorItem.INT3_RP_2_SMART_COM_CODE = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_1_DF_SEQ")
                            {
                                doorItem.SB_RP_1_DF_SEQ = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_2_DF_SEQ")
                            {
                                doorItem.SB_RP_2_DF_SEQ = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_1_DF_SEQ")
                            {
                                doorItem.INT1_RP_1_DF_SEQ = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_2_DF_SEQ")
                            {
                                doorItem.INT1_RP_2_DF_SEQ = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_1_DF_SEQ")
                            {
                                doorItem.INT2_RP_1_DF_SEQ = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_2_DF_SEQ")
                            {
                                doorItem.INT2_RP_2_DF_SEQ = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_1_DF_SEQ")
                            {
                                doorItem.INT3_RP_1_DF_SEQ = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_2_DF_SEQ")
                            {
                                doorItem.INT3_RP_2_DF_SEQ = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_1_WIDTH_CODE")
                            {
                                doorItem.SB_RP_1_WIDTH_CODE = item.Value[0].name;
                            }
                            if (item.name == "SB_RP_2_WIDTH_CODE")
                            {
                                doorItem.SB_RP_2_WIDTH_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_1_WIDTH_CODE")
                            {
                                doorItem.INT1_RP_1_WIDTH_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT1_RP_2_WIDTH_CODE")
                            {
                                doorItem.INT1_RP_2_WIDTH_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_1_WIDTH_CODE")
                            {
                                doorItem.INT2_RP_1_WIDTH_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT2_RP_2_WIDTH_CODE")
                            {
                                doorItem.INT2_RP_2_WIDTH_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_1_WIDTH_CODE")
                            {
                                doorItem.INT3_RP_1_WIDTH_CODE = item.Value[0].name;
                            }
                            if (item.name == "INT3_RP_2_WIDTH_CODE")
                            {
                                doorItem.INT3_RP_2_WIDTH_CODE = item.Value[0].name;
                            }                           

                        }
                    }
                }
                if (input1 && input2)
                {
                    doorItem.Exclude = true;
                }

                // Set hold code to E2 if Syspro sales order reference number starts with 5
                if (ewOrder.SysproSalesOrder.StartsWith("5"))
                {
                    doorItem.HoldOrdersCode = "E2";
                }

                // Add the door details and all the parts to the internal structure
                if (detail.Bom != null)
                {
                    foreach (var bom in detail.Bom.Bom1)
                    {
                        orderItem1 = null;
                        if (ewOrder.OrderType == "B") // If order type = B get lot number
                        {
                            doornum++;
                            result = SendWebApiMessage(apiUrl + "GetLotNumber", "").Result;
                            if (result != null)
                            {
                                lot = JsonConvert.DeserializeObject<LotNumberReturn>(result);
                                lotnumber = lot.szString30A;
                                lotflag = true;
                                ewOrder.LotNumber = lotnumber;
                            }
                        }
                        if (bom.Bom1 != null)
                        {
                            // Add the accessories header 
                            if (bom.SMARTPART_NUM == "OPTIONS_ACCESS_HEADER")
                            {
                                itemMaster = detail.ItemMaster.Where(x => x.ITEM_NUM == bom.ITEM_NUM).FirstOrDefault();
                                optionacc = itemMaster.DESCRIPTION;
                            }
                            // Add the main items
                            foreach (var bom1 in bom.Bom1)
                            {
                                itemMaster = detail.ItemMaster.Where(x => x.ITEM_NUM == bom1.ITEM_NUM).FirstOrDefault(); // Get the item details
                                //if (itemMaster.VAR_18 == "Y" && validYLineParts.Contains(itemMaster.ITEM_NUM)) // If the door item get a lot number from JDE and check valid y line list for part
                                if (validYLineParts.Contains(itemMaster.ITEM_NUM)) // If the door item get a lot number from JDE and check valid y line list for part
                                {
                                    ewOrder.YLineItem = itemMaster.ITEM_NUM;
                                    doornum++;
                                    result = SendWebApiMessage(apiUrl + "GetLotNumber", "").Result;
                                    if (result != null)
                                    {
                                        lot = JsonConvert.DeserializeObject<LotNumberReturn>(result);
                                        lotnumber = lot.szString30A;
                                        lotflag = true;
                                        ewOrder.LotNumber = lotnumber;
                                    }
                                }
                                // Set the window quantity
                                if ((bom1.ITEM_NUM.StartsWith("3G") || bom1.ITEM_NUM.StartsWith("4")) && Char.IsLetter(Convert.ToChar(bom1.ITEM_NUM.Substring(2, 1))))
                                {
                                    doorItem.WindowQuantity = Convert.ToInt32(bom1.QUANTITY);
                                }
                                linetype = itemMaster.VAR_18; // Gets the linetype from easy web if set
                                easyweblinetype = itemMaster.VAR_18; // Gets the linetype from easy web if set
                                if (string.IsNullOrWhiteSpace(linetype))
                                {
                                    linetype = _jde.GetField("SELECT imlnty FROM CRPDTA.F4101 where imprp0 = '000500' and imlitm = '" + itemMaster.ITEM_NUM + "'");
                                }
                                lotflag = linetype == "W" || linetype == "T" || linetype == "Y" || linetype == "7" ? true : false;
                                orderItem1 = GetOrderItem(bom1, itemMaster, bom.ITEM_NUM, detail, ewOrder, configuration, adderPrefix, lotnumber, linetype, easyweblinetype, line, doornum, lotflag, true, out linenum);
                                line = linenum;
                                //if (itemMaster.VAR_18 == "Y" && validYLineParts.Contains(itemMaster.ITEM_NUM)) // If the door item get VAR_25
                                if (validYLineParts.Contains(itemMaster.ITEM_NUM)) // If the door item get VAR_25
                                {
                                    // Get VAR_25 for the create PO
                                    if (ewOrder.CreatePO && !string.IsNullOrWhiteSpace(itemMaster.VAR_25))
                                    {
                                        ewOrder.Var25.Add(itemMaster.VAR_25 + "~" + linenum.ToString());
                                    }
                                }                               
                                //if (lotflag) lotflag = false; // Only get the lot number for the door item
                                //if (orderItem.Discount != 0)
                                //{
                                //    discountItem = new OrderItem()
                                //    {
                                //        ItemNum = discountItemNum,
                                //        Description = orderItem.Discount  + "% Discount",
                                //        UnitPrice = -(bom2.DISCOUNT_AMT),
                                //        Quantity = bom2.QUANTITY,
                                //        LineNum = line++,
                                //        RefLineNum = orderItem.LineNum,
                                //        LineType = "PD",
                                //        LotNumber = lotnumber
                                //    };
                                //}
                                // If a door item add the door details to the item
                                //if (itemMaster.VAR_18 == "Y" && validYLineParts.Contains(itemMaster.ITEM_NUM))
                                if (validYLineParts.Contains(itemMaster.ITEM_NUM)) // If the door item get VAR_25
                                {
                                    orderItem1.Door = new DoorInfo()
                                    {
                                        Model = "",
                                        HeightFt = "",
                                        HeightIn = "",
                                        WidthFt = "",
                                        WidthIn = "",
                                        Colour = ""
                                    };
                                    foreach (var input in detail.Input)
                                    {
                                        switch (input.name)
                                        {
                                            case "DOOR_MODEL":
                                                orderItem1.Door.Model = input.Value[0].label.Trim().Replace(" ", "");
                                                break;
                                            case "DOOR_WIDTH_FT":
                                                orderItem1.Door.WidthFt = input.Value[0].label;
                                                break;
                                            case "OPENING_WIDTH_FEET":
                                                orderItem1.Door.WidthFt = input.Value[0].label;
                                                break;
                                            case "DOOR_WIDTH_IN":
                                                orderItem1.Door.WidthIn = input.Value[0].label;
                                                break;
                                            case "OPENING_WIDTH_INCHES":
                                                orderItem1.Door.WidthIn = input.Value[0].label;
                                                break;
                                            case "DOOR_HEIGHT_FT":
                                                orderItem1.Door.HeightFt = input.Value[0].label;
                                                break;
                                            case "OPENING_HEIGHT_FEET":
                                                orderItem1.Door.HeightFt = input.Value[0].label;
                                                break;
                                            case "DOOR_HEIGHT_IN":
                                                orderItem1.Door.HeightIn = input.Value[0].label;
                                                break;
                                            case "SECTION_HEIGHT_IN":
                                                orderItem1.Door.HeightIn = input.Value[0].label;
                                                break;
                                            case "OPENING_HEIGHT_INCHES":
                                                orderItem1.Door.HeightIn = input.Value[0].label;
                                                break;
                                            case "DOOR_COLOUR":
                                                orderItem1.Door.Colour = input.Value[0].label;
                                                break;
                                        }
                                    }
                                }
                                // If the main item has bom components add the items and bom data to the internal structure
                                if (bom1.Bom1 != null)
                                {
                                    if (orderItem1.ItemNum == bom1.ITEM_NUM)
                                    {
                                        foreach (var bom2 in bom1.Bom1)
                                        {
                                            // Add the component
                                            itemMaster = detail.ItemMaster.Where(x => x.ITEM_NUM == bom2.ITEM_NUM).FirstOrDefault();
                                            linetype = adderPrefix.Any(x => bom2.ITEM_NUM.StartsWith(x)) ? "PA" : itemMaster.VAR_18; // Gets the linetype from easy web if set
                                            easyweblinetype = adderPrefix.Any(x => bom2.ITEM_NUM.StartsWith(x)) ? "PA" : itemMaster.VAR_18; // Gets the linetype from easy web if set
                                            if (string.IsNullOrWhiteSpace(linetype))
                                            {
                                                linetype = _jde.GetField("SELECT imlnty FROM CRPDTA.F4101 where imprp0 = '000500' and imlitm = '" + bom2.ITEM_NUM + "'");
                                            }
                                            lotflag = linetype == "W" || linetype == "T" || linetype == "7" ? true : false;
                                            orderItem2 = GetOrderItem(bom2, itemMaster, bom1.ITEM_NUM, detail, ewOrder, configuration, adderPrefix, lotnumber, linetype, easyweblinetype, line, doornum, lotflag, true, out linenum);
                                            line = linenum;
                                            //if (discount != 0)
                                            //{
                                            //    ewOrder.Items.Add(new OrderItem()
                                            //    {
                                            //        ItemNum = discountItemNum,
                                            //        Description = discount + "% Discount",
                                            //        UnitPrice = -(bom3.DISCOUNT_AMT),
                                            //        Quantity = bom3.QUANTITY,
                                            //        LineNum = line++,
                                            //        RefLineNum = linenum,
                                            //        LineType = "PD",
                                            //        LotNumber = orderItem.LotNumber
                                            //    });
                                            //}
                                            // Add the bom data
                                            if (bom2.ITEM_NUM.Trim().ToLower() != "comment")
                                            {
                                                orderItem1.BOMs.Add(new BOM() { ItemNum = bom2.ITEM_NUM, Quantity = bom2.QUANTITY.ToString(), Branch = "50000", CutInstPart = bom1.ITEM_NUM, CutInstructions = new List<string>() });
                                            }

                                            if (bom2.Bom1 != null)
                                            {
                                                foreach (var bom3 in bom2.Bom1)
                                                {
                                                    if (bom3.ITEM_NUM.Trim().ToLower() != "comment")
                                                    {
                                                        // Add the component
                                                        itemMaster = detail.ItemMaster.Where(x => x.ITEM_NUM == bom3.ITEM_NUM).FirstOrDefault();
                                                        easyweblinetype = adderPrefix.Any(x => bom3.ITEM_NUM.StartsWith(x)) ? "PA" : itemMaster.VAR_18; // Gets the linetype from easy web if set
                                                        linetype = _jde.GetField("SELECT imlnty FROM CRPDTA.F4101 where imprp0 = '000500' and imlitm = '" + bom3.ITEM_NUM + "'");
                                                        lotflag = linetype == "W" || linetype == "T" || linetype == "7" ? true : false;
                                                        orderItem3 = GetOrderItem(bom3, itemMaster, bom2.ITEM_NUM, detail, ewOrder, configuration, adderPrefix, lotnumber, linetype, easyweblinetype, line, doornum, lotflag, false, out linenum);
                                                        line = linenum;
                                                        // Add the bom data
                                                        if (bom3.ITEM_NUM.Trim().ToLower() != "comment")
                                                        {
                                                            orderItem2.BOMs.Add(new BOM() { ItemNum = bom3.ITEM_NUM, Quantity = bom3.QUANTITY.ToString(), Branch = "50000", CutInstPart = bom2.ITEM_NUM, CutInstructions = new List<string>() });
                                                        }

                                                        if (bom3.Bom1 != null)
                                                        {
                                                            foreach (var bom4 in bom3.Bom1)
                                                            {
                                                                if (bom4.ITEM_NUM.Trim().ToLower() != "comment")
                                                                {
                                                                    // Add the bom data
                                                                    orderItem3.BOMs.Add(new BOM() { ItemNum = bom4.ITEM_NUM, Quantity = bom4.QUANTITY.ToString(), Branch = "50000", CutInstPart = bom3.ITEM_NUM, CutInstructions = new List<string>() });
                                                                }
                                                            }
                                                        }
                                                        doorItem.Items.Add(orderItem3);
                                                    }
                                                }
                                            }
                                            doorItem.Items.Add(orderItem2);
                                        }
                                    }
                                }
                                orderItem1.RoutingTextList = AddRoutingDetails(orderItem1.ItemNum, configuration);
                                doorItem.Items.Add(orderItem1);
                            }
                        }
                    }
                }

                // Order the items by their line number
                doorItem.Items = doorItem.Items.OrderBy(x => x.LineNum).ToList();

                // Check for item descriptions longer than 30 characters and return an error
                foreach (var item in doorItem.Items)
                {
                    if (item.Description.Length > 30)
                    {
                        ewOrder.Error += item.ItemNum + " - " + item.Description + " has a description that is > 30 characters\r\n";
                    }
                }
                // If no items have a lotnumber then there is no valid Y line - return an error
                if (string.IsNullOrWhiteSpace(lotnumber))
                {
                    ewOrder.Error += "No items with Y linetype\r\n";
                }

                // Add accessory items to the internal structure
                if (!string.IsNullOrWhiteSpace(optionacc))
                {
                    doorItem.Items.Add(new OrderItem()
                    {
                        ItemNum = "",
                        UOM = "",
                        Description = optionacc,
                        LineNum = 0,
                        LotNumber = "",
                        LineType2 = ""
                    });
                }
            }
            //if (detail.TYPE == "A")
            //{
            //    ewOrder.Freight = Math.Round(detail.UNIT_PRICE, 2);
            //    ewOrder.Items.Add(new OrderItem() { ItemNum = "", UOM = "", Description = detail.DESCRIPTION, Quantity = 1, UnitPrice = 0, LineNum = 0 });
            //}
            // If the order has just single items add them to the internal structure
            if (detail.TYPE == "S")
            {
                if (doorItem == null)
                {
                    // Initialize the internal structure with door structures 
                    doorItem = new DoorItem() { Items = new List<OrderItem>() };
                    ewOrder.DoorItems.Add(doorItem);
                }

                // Add the component
                itemMaster = detail.ItemMaster.Where(x => x.ITEM_NUM == detail.ITEM_NUM).FirstOrDefault();
                linetype = adderPrefix.Any(x => detail.ITEM_NUM.StartsWith(x)) ? "PA" : itemMaster.VAR_18; // Gets the linetype from easy web if set
                orderItem3 = GetOrderItem(null, itemMaster, "", detail, ewOrder, configuration, adderPrefix, "", linetype, linetype, line, doornum, false, true, out linenum);
                line = linenum;
                //if (discount != 0)
                //{
                //    ewOrder.Items.Add(new OrderItem()
                //    {
                //        ItemNum = discountItemNum,
                //        Description = discount + "% Discount",
                //        UnitPrice = -(detail.DISCOUNT_AMT),
                //        Quantity = detail.QUANTITY,
                //        LineNum = line++,
                //        RefLineNum = linenum,
                //        LineType = "PD",
                //        LotNumber = ""
                //    });
                //}
                doorItem.Items.Add(orderItem3);
            }
        }
        // Save the internal order structure and data as a JSON file and save it to a file
        string ewOrderJson = JsonConvert.SerializeObject(ewOrder);
        StreamWriter sw = new StreamWriter("c:\\jdelog\\raynorjdeapiorderitems_" + ewOrder.SerialNum.Replace(" ", "") + "_" + DateTime.Now.ToString("MMddyyyymmss") + ".txt", false);
        sw.Write(ewOrderJson);
        sw.Flush();
        sw.Close();

        return ewOrder;
    }

    // Add the item to the order for JDE
    private OrderItem GetOrderItem(Bom bom, ItemMaster itemMaster, string masterItem, Detail detail, EWOrder ewOrder, Configuration configuration, string[] adderPrefix, string lotnumber, string linetype, string easyweblinetype, int line, int doornum, bool lotflag, bool lineflag, out int newline)
    {
        // For testing only
        if (itemMaster.ITEM_NUM == "LARW-18050-SASN")
        {
            string s = "";
        }
        string stockingtype = itemMaster.VAR_21;
        string itemweight = "";

        // For development get item details from the database
        if (_environment == "dev")
        {
            // Get line, stocking type and item weight from item master database and override the line type from Var_18
            int dataRows = _db.GetTable("SELECT lnType,stkgType,itemWeight FROM dbo.SysproToJdeItemMaster WHERE stkCode='" + bom.ITEM_NUM + "'", "Item");
            if (dataRows > 0)
            {
                linetype = _db.DSet.Tables["Item"].Rows[0][0].ToString().Trim();
                stockingtype = _db.DSet.Tables["Item"].Rows[0][1].ToString().Trim();
                itemweight = _db.DSet.Tables["Item"].Rows[0][2].ToString().Trim();
            }
        }

        // If item weight not in item master database use weight from easy web
        if (string.IsNullOrEmpty(itemweight))
        {
            itemweight = itemMaster.WEIGHT;
        }

        // Get the rest item values
        string itemnum;
        string description;
        float quantity;
        double unitprice;
        double discount;
        int linenum;
        string taxable;
        string linetype2;
        if (bom != null)
        {
            itemnum = bom.ITEM_NUM;
            description = itemMaster.DESCRIPTION.Length <= 30 ? itemMaster.DESCRIPTION : itemMaster.DESCRIPTION.Substring(0, 30);
            quantity = bom.QUANTITY * detail.QUANTITY;
            unitprice = bom.UNIT_PRICE;
            discount = bom.DISCOUNT_AMT != 0 ? Math.Round((bom.DISCOUNT_AMT / bom.UNIT_PRICE) * 100, 1) : 0;
            if (lineflag)
            {
                linenum = adderPrefix.Any(x => bom.ITEM_NUM.StartsWith(x)) && bom.UNIT_PRICE == 0 ? 0 : line++;
            }
            else
            {
                linenum = 0;
            }
            
            taxable = bom.UNIT_PRICE == 0 ? "" : ewOrder.Country == "CA" ? "Y" : "N";
            linetype2 = "Y" + doornum.ToString();
        }
        else
        {
            itemnum = detail.ITEM_NUM;
            description = detail.DESCRIPTION.Length <= 30 ? detail.DESCRIPTION : detail.DESCRIPTION.Substring(0, 30);
            quantity = detail.QUANTITY;
            unitprice = detail.UNIT_PRICE;
            discount = detail.DISCOUNT_AMT != 0 ? Math.Round((detail.DISCOUNT_AMT / detail.UNIT_PRICE) * 100, 1) : 0;
            if (lineflag)
            {
                linenum = adderPrefix.Any(x => detail.ITEM_NUM.StartsWith(x)) && detail.UNIT_PRICE == 0 ? 0 : line++;
            }
            else
            {
                linenum = 0;
            }
            taxable = detail.UNIT_PRICE == 0 ? "" : ewOrder.Country == "CA" ? "Y" : "N";
            linetype2 = "";
        }

        // Add the item to the internal structure 
        OrderItem orderItem = new OrderItem()
        {
            ItemNum = itemnum,
            Description = description,
            UOM = itemMaster.VAR_4,
            BOMs = new List<BOM>(),
            Quantity = quantity,
            UnitPrice = unitprice,
            Discount = discount,
            LineNum = linenum,
            LineType = linetype,
            StockingType = stockingtype,
            LotNumber = lotflag ? lotnumber : "",
            Weight = itemweight,
            Taxable = taxable,
            LineType2 = linetype2,
            CommodityCode = itemMaster.VAR_15,
            SalesCat1 = itemMaster.VAR_11,
            SalesCat2 = itemMaster.VAR_12,
            SalesCat3 = itemMaster.VAR_13,
            SalesCat4 = itemMaster.VAR_14,
            MasterPlanFamily = itemMaster.VAR_17,
            StockRunCode = itemMaster.VAR_16,
            CutInstPart = masterItem,
            CutInstructions = new List<string>(),
            RoutingTextList = AddRoutingDetails(itemnum, configuration),
            // The following values are not required for manufactured items from EasyWeb
            BuyerNumber = itemMaster.VAR_28,
            LeadTime = "",
            VendorNum = "",
            SmartPartNum = itemMaster.SMARTPART_NUM,
            Supplier = itemMaster.VAR_24,
            ItemCost = itemMaster.VAR_27,
            ComRunTime = _db.GetField("select LabHr from InvPartsRunTime where StockCode='" + itemMaster.ITEM_NUM + "'"),
            EasyWebLineType = easyweblinetype,
            VAR_29 = itemMaster.VAR_29
        };

        // Return the updated line number
        newline = line;

        return orderItem;
    }

    // Process order for JDE
    private void ProcessEWOrder(ConceptAccessAPIClient conceptAccess, EWOrder ewOrder, string in0, OrderInfo orderInfo)
    {
        
        // Get the JDE urls
        string apiUrl = _configuration.GetValue<string>("AppSettings:ApiUrl"); // Main url for JDE web api
        string apiUrl2 = _configuration.GetValue<string>("AppSettings:ApiUrl2"); // JDE web api url for saving text
        // Get the development or production endpoint
        if (_environment == "dev")
        {
            conceptAccess.Endpoint.Address = new System.ServiceModel.EndpointAddress(_configuration.GetValue<string>("AppSettings:EasyWebEnvDev"));
        }
        else if(_environment == "test")
        {
            conceptAccess.Endpoint.Address = new System.ServiceModel.EndpointAddress(_configuration.GetValue<string>("AppSettings:EasyWebEnvTest"));
        }
        else
        {
            conceptAccess.Endpoint.Address = new System.ServiceModel.EndpointAddress(_configuration.GetValue<string>("AppSettings:EasyWebEnvProd"));
        }
        string result = AddMissingItems(ewOrder, apiUrl); // Process to add missing parts in JDE

        if (result != null && result != "error")
        //ys    if (result != null)

        {
            result = AddOrderAndDetail(ewOrder, orderInfo, apiUrl); // Process to add order details and header in JDE
            if (string.IsNullOrWhiteSpace(result)) // Check for a successful update
            {
                foreach (var doorItem in ewOrder.DoorItems)
                {
                    ewOrder.Submitted = true; // Set the order submitted to JDE to true
                }
                AddBomAndRouting(ewOrder, apiUrl, apiUrl2); // Process to add bom and routing in JDE
                AddOrderItemText(ewOrder, apiUrl2); // Process to add order text in JDE
            }
            else
            {
                WriteLog(result);
            }
        }
        else
        {
            result = "Error: Add missing items failed"; // Missing items not added in JDE
        }

        // As long as the order header was successfully added update the order in EasyWeb with the JDE sales order
        if (!string.IsNullOrWhiteSpace(result))
        {
            // An error was returned when the order was submitted to JDE
            WriteLog("EasyWeb Order " + in0 + " Not Updated in JDE");
            SendMail("JDE Order Submission Error", "EasyWeb Order " + in0 + " Not Updated in JDE", emailIT);
        }
        else
        {
            // Order successfully submitted
            SendMail("JDE Order Submission Success", "EasyWeb Order " + in0 + " Updated JDE order " + ewOrder.SalesOrder, emailIT);
           
           if (ewOrder.SPR == "Y")
            {
                SendSPRMail("Configure One Notification for " + "EasyWeb Order" + in0  + " ( " + ewOrder.ConfigReference + " ) " + " with SO# " + ewOrder.SalesOrder, ewOrder.SPR_DETAIL,emailSpr);
            }
           

           
            if (ewOrder.CreatePO)
            {
                try
                {
                    StreamWriter sw;
                    int rows;
                    string[] details;
                    string lotNumber;
                    string yGroup;

                    // Created a file if required to process the create PO
                    if (ewOrder.Var25.Count > 1)
                    {
                        int ext;
                        for (int i = 0; i < ewOrder.Var25.Count; i++)
                        {
                            ext = i + 1;
                            sw = new StreamWriter(createPOFolder + ewOrder.SalesOrder + "_" + ext.ToString() + ".txt", true);
                            if (sw != null)
                            {
                                string itemNums = "";
                                // Get the YGroup data to add to the lot number when there are multiple lines.
                                details = ewOrder.Var25[i].Split("~");
                                lotNumber = ewOrder.LotNumber;
                                yGroup = "";
                                rows = _jde.GetTable("SELECT SZLOTN,SZIR03 FROM CRPDTA.F47012 WHERE SZKCOO='00500' AND SZDOCO=" + ewOrder.SalesOrder + " AND SZDCTO='SO' AND SZLNID=" + details[1] + "000", "LineDetails");
                                if (rows > 0)
                                {
                                    lotNumber = _jde.DSet.Tables["LineDetails"].Rows[0][0].ToString().Trim();
                                    yGroup = _jde.DSet.Tables["LineDetails"].Rows[0][1].ToString().Trim();
                                }
                                sw.WriteLine(ewOrder.SalesOrder + "," + lotNumber + "," + yGroup + "," + ewOrder.Var25[i]) ;
                                if (ewOrder.ExcludedItem.Count > 0)
                                {
                                    itemNums = ewOrder.ExcludedItem[0];
                                }
                                sw.WriteLine(itemNums);
                                sw.Flush();
                                sw.Close();
                                sw.Dispose();
                            }
                        }
                    }
                    else
                    {
                        sw = new StreamWriter(createPOFolder + ewOrder.SalesOrder + ".txt", true);
                        if (sw != null)
                        {
                            string itemNums = "";
                            // Get the YGroup data to add to the lot number when there are multiple lines.
                            details = ewOrder.Var25[0].Split("~");
                            lotNumber = ewOrder.LotNumber;
                            yGroup = "";
                            rows = _jde.GetTable("SELECT SZLOTN,SZIR03 FROM CRPDTA.F47012 WHERE SZKCOO='00500' AND SZDOCO=" + ewOrder.SalesOrder + " AND SZDCTO='SO' AND SZLNID=" + details[1] + "000", "LineDetails");
                            if (rows > 0)
                            {
                                lotNumber = _jde.DSet.Tables["LineDetails"].Rows[0][0].ToString().Trim();
                                yGroup = _jde.DSet.Tables["LineDetails"].Rows[0][1].ToString().Trim();
                            }
                            sw.WriteLine(ewOrder.SalesOrder + "," + lotNumber + "," + yGroup + "," + ewOrder.Var25[0]);
                            if (ewOrder.ExcludedItem.Count > 0)
                            {
                                itemNums = ewOrder.ExcludedItem[0];
                            }
                            sw.WriteLine(itemNums);
                            sw.Flush();
                            sw.Close();
                            sw.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteLog("Error in jde po add process - " + ex.Message);
                }
            }
            WriteLog("Returning from jde order submission");
            string sPO;
            string sSQL;
            string sSQLCommand = "";
            string proc = "0";
         //   string jdeSoType = "SO";
            string jdeSoType = strOrderStatus;


            List<OrderItem> doorItems;
            try
            {
                // Update the database for Syspro orders - can be removed when Syspro order process is complete
                if (ewOrder.SysproSalesOrder.StartsWith("5"))
                {
                    foreach (var doorItem in ewOrder.DoorItems)
                    {
                        foreach (var item in doorItem.Items)
                        {
                            if (item.LineType == "Y")
                            {
                                if (_syspro.GetTable("select PurchaseOrder from PorMasterDetail where LineType='6' and NComment like '%" + ewOrder.SysproSalesOrder + "%'", "Items") > 0)
                                {
                                    sPO = _syspro.DSet.Tables["Items"].Rows[0][0].ToString().Trim();
                                    _syspro.ExecuteCommand("update PorMasterHdr set User1='A' where PurchaseOrder='" + sPO + "'");
                                    for (int i = 0; i < _syspro.DSet.Tables["Items"].Rows.Count; i++)
                                    {
                                        sPO = _syspro.DSet.Tables["Items"].Rows[i][0].ToString().Trim();
                                        sSQL = "select MStockCode,Line from PorMasterDetail where LineType='1' and PurchaseOrder='" + sPO + "' and (MStockDes like 'GF%' or MStockDes like 'DF%' or MStockDes like 'CF%')";
                                        if (_syspro.GetTable(sSQL, "Item") > 0)
                                        {
                                            for (int i2 = 0; i2 < _syspro.DSet.Tables["Item"].Rows.Count; i2++)
                                            {
                                                doorItems = doorItem.Items.Where(x => x.ItemNum.StartsWith("RL")).ToList();
                                                foreach (var item2 in doorItems)
                                                {
                                                    sSQLCommand += "insert into jdePO (stockCode,poNum,sysproOrder,jdeOrder,lotNumber,genericItem,jdeOrderline,jdeOrderType,recProcessed,sysproOrderLine) VALUES('" + _syspro.DSet.Tables["Item"].Rows[i2][0].ToString().Trim() + "',";
                                                    sSQLCommand += sPO + "," + ewOrder.SysproSalesOrder + "," + ewOrder.SalesOrder + "," + item.LotNumber + ", '" + item2.ItemNum + "', '" + item2.LineNum + "','" + jdeSoType + "'," + proc + ",'" + _syspro.DSet.Tables["Item"].Rows[i2][1].ToString().Trim() + "');";
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (sSQLCommand != "")
                    {
                        _db.ExecuteCommand(sSQLCommand);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog("Error in jde po add process - " + ex.Message);
            }
            WriteLog("Returning from jde po add process");
        }
    }



    // Add item routing in JDE

    private List<RoutingText> AddRoutingDetails(string itemNum, Configuration configuration)
    {
          
        List<RoutingText> routingTextList = null;

        // Check if routing configuration data is available
        if (configuration != null)
        {
            if (configuration.Routing != null)
            {
                RoutingText routingText;
                string workCenter = "";
                string runLabour = "";
                string operSequence = "";
                string sequence_num = "" ;
                // Loop through the routing array and call endpoint with the item routing data

                foreach (var config in configuration.Routing)
                {
                    if (config.SMARTPART_NUM == itemNum)
                    {
                        routingTextList = new List<RoutingText>();
                        routingText = new RoutingText() { ItemNum = config.SMARTPART_NUM, Operations = new List<RoutingOperation>() };
                            foreach (var operation in config.Operation)
                            {
                                foreach (var param in operation.OperationParam)
                                {
                                    switch (param.DESCRIPTION)
                                    {
                                        case "Run Time":
                                            runLabour = param.VALUE;
                                            break;
                                        case "WorkCenter":
                                            workCenter = param.VALUE;
                                            break;
                                        case "Sequence Number":
                                            operSequence = param.VALUE;
                                            break;
                                    }
                                }
                                if (!routingText.Operations.Any(x => x.WorkCenter == workCenter))
                                {
                                    if (operSequence != "0")
                                    {
                                        routingText.Operations.Add(new RoutingOperation() { SequenceNumber = operSequence, Description = operation.DESCRIPTION, WorkCenter = workCenter, RunLabor = runLabour });
                                    }
                                }
                                if (routingText.Operations.Count > 0 && operSequence != "0")
                                {
                                    routingTextList.Add(routingText);
                                }
                            }
                    }
                }

            }
        }
        return routingTextList;
    }

    // Add Missing Items Process - ** This function does not add item cost records in JDE - look into the master update program for the code **
    private string AddMissingItems(EWOrder ewOrder, string apiUrl)
    {
        string result = "";
        string json;
        DateTime dtNow;
        string date;
        string part;
        ItemPriceUpdate itemPrice;
        string priceEffEndDate = _configuration.GetValue<string>("AppSettings:PriceEffEndDate");
        string[] itemExclude = _configuration.GetValue<string>("AppSettings:ItemExclude").Split(',');
        string[] pctExclude = _configuration.GetValue<string>("AppSettings:PctPartExclude").Split(',');
        //        double unitPrice;
        ItemQuery itemQuery;
        OrderItem cutOrderItem = null;
        foreach (var doorItem in ewOrder.DoorItems)
        {
            itemQuery = new ItemQuery() { ItemArray = new List<Item>(), webOrderNbr = "", configurationNbr = 0 };
            // Get list of items to check their existance in JDE 
            foreach (var item in doorItem.Items)
            {
                // For testing only
                if (item.ItemNum == "PCT2H-D108000902-SBTVL")
                {
                    string s = "";
                }
                if (!string.IsNullOrWhiteSpace(item.ItemNum) && item.ItemNum != "Freight" && !item.ItemNum.StartsWith("Cut To Instruction") && !item.ItemNum.EndsWith("-CUT") && !pctExclude.Any(x => item.ItemNum.StartsWith(x)))
                {
                    if (!itemQuery.ItemArray.Any(x => x.szIdentifier2ndItem == item.ItemNum))
                    {
                        itemQuery.ItemArray.Add(new Item() { szIdentifier2ndItem = item.ItemNum, sz55ErrorDescription = "" });
                    }
                }
                // For cut instructions remove the -CUT to get the item number
                if (item.ItemNum.EndsWith("-CUT"))
                {
                    part = item.ItemNum.Replace("-CUT", "").Trim();
                    cutOrderItem = doorItem.Items.Where(x => x.ItemNum == part).FirstOrDefault();
                    if (cutOrderItem != null)
                    {
                        if (cutOrderItem.CutInstructions != null)
                        {
                            cutOrderItem.CutInstructions.Add(item.Description);
                        }
                    }
                }
                if (item.BOMs != null)
                {
                    // Get all the bom items
                    foreach (var item2 in item.BOMs)
                    {
                        if (!item2.ItemNum.StartsWith("Cut To Instruction") && !item2.ItemNum.EndsWith("-CUT") && !pctExclude.Any(x => item2.ItemNum.StartsWith(x)) && !itemQuery.ItemArray.Any(x => x.szIdentifier2ndItem.Contains(item.ItemNum)))
                        {
                            itemQuery.ItemArray.Add(new Item() { szIdentifier2ndItem = item2.ItemNum, sz55ErrorDescription = "" });
                        }
                        if (item2.BOMs != null)
                        {
                            foreach (var item3 in item2.BOMs)
                            {
                                if (!item3.ItemNum.StartsWith("Cut To Instruction") && !item3.ItemNum.EndsWith("-CUT") && !pctExclude.Any(x => item3.ItemNum.StartsWith(x)) && !itemQuery.ItemArray.Any(x => x.szIdentifier2ndItem.Contains(item.ItemNum)))
                                {
                                    itemQuery.ItemArray.Add(new Item() { szIdentifier2ndItem = item3.ItemNum, sz55ErrorDescription = "" });
                                }
                            }
                        }
                    }
                }
            }
            result = "";
            // Set the error message body to be emailed if any items do not have the MPF code
            string body = "For order " + EWOrderNum + " the following items does not have MPF code:";
            OrderItem orderItem;
            if (itemQuery.ItemArray.Count > 0)
            {
                ItemQueryReturn items = AddNewItem(itemQuery, doorItem, apiUrl);
                foreach (var item in items.N554130_Repeating)
                {
                    foreach (var item1 in ewOrder.DoorItems)
                    {
                        orderItem = item1.Items.Where(x => x.ItemNum == item.szIdentifier2ndItem && !string.IsNullOrWhiteSpace(item.sz55ErrorDescription)).FirstOrDefault();
                        if (orderItem != null)
                        {
                            if (orderItem.StockingType == "M" && !orderItem.ItemNum.StartsWith("Z") && string.IsNullOrWhiteSpace(orderItem.MasterPlanFamily))
                            {
                                result = "error";
                                body += " " + orderItem.ItemNum;
                                WriteLog("MPF error for part " + orderItem.ItemNum);
                            }
                        }
                    }
                }
                if (result != "")
                {
                    SendMail("EasyWeb Order Missing MPF " + EWOrderNum, body, emailIT);
                    break;
                }
            }

            // Add a base item price for all items with a unit price
            dtNow = DateTime.Now;
            date = dtNow.Month.ToString() + "/" + dtNow.Day.ToString() + "/" + dtNow.Year.ToString();
            foreach (var item in doorItem.Items)
            {
                if (!string.IsNullOrWhiteSpace(item.ItemNum) && item.ItemNum != "Freight" && item.UnitPrice != 0 && item.LineType != "Y")
                {
                    itemPrice = new ItemPriceUpdate() { LongItemNumber = item.ItemNum, GridArray = new List<RaynorJdeApi.Models.ItemPrice>() };
                    itemPrice.GridArray.Add(new RaynorJdeApi.Models.ItemPrice()
                    {
                        UM = item.UOM,
                        Unit_Price = item.UnitPrice.ToString("0.00"),
                        Eff_Date_From = date,
                        Eff_Date_Thru = priceEffEndDate,
                        Currency_Code = ewOrder.Country.ToLower() == "ca" ? "CAD" : "USD",
                        Branch_Plant = "50000"
                    });
                    json = JsonConvert.SerializeObject(itemPrice);
                    _ = SendWebApiMessage(apiUrl + "AddItemBasePrice", json);
                }
            }
        }
        return result;
    }

    // Add new item to jde
    private ItemQueryReturn AddNewItem(ItemQuery itemQuery, DoorItem doorItem, string apiUrl)
    {
        ItemQueryReturn items = new ItemQueryReturn() { N554130_Repeating = new List<Item>() };
        string result;
        string json = JsonConvert.SerializeObject(itemQuery);

        // Call JDE to check for missing items
        result = SendWebApiMessage(apiUrl + "N554130", json).Result;
        if (result != null)
        {
            items = JsonConvert.DeserializeObject<ItemQueryReturn>(result);
            OrderItem orderItem = null;
            ItemUpdate itemUpdate = null;
            Result result1;
            string discountItemNum = _configuration.GetValue<string>("AppSettings:DiscountItemNum");
            string uom = "";
            string buyerNumber = "";
            string leadTime = "";
            string lineType = "";
            string commodityCode = "";
            string stockingType = "";
            string salesCat1 = "";
            string salesCat2 = "";
            string salesCat3 = "";
            string salesCat4 = "";
            string masterplanfamily = "";
            string stockruncode = "";
            string vendornum = "";
            string glClass = "";
            // Loop through the returned item list and check for the missing item error
            foreach (var item in items.N554130_Repeating)
            {
                orderItem = doorItem.Items.Where(x => x.ItemNum == item.szIdentifier2ndItem && item.szIdentifier2ndItem != discountItemNum).FirstOrDefault(); // Get the order item
                if (orderItem != null)
                {
                    switch (_environment)
                    {
                        // Set item values from database when in development mode
                        case "dev":
                            if (_db.GetTable("SELECT stkCode,SalesCat1,SalesCat2,SalesCat3,salesCat4,mpfPrp1,lnType,stkgType,buyerCdJde,leadTime,stkRunCd,mpfPrp4,VendorNum FROM dbo.SysproToJdeItemMaster WHERE stkCode='" + orderItem.ItemNum + "'", "Item") > 0) // Get stockcode details from database
                            {
                                uom = _syspro.GetField("SELECT StockUom FROM dbo.InvMaster WHERE StockCode='" + orderItem.ItemNum + "'").Trim();
                                uom = uom != "SQF" ? uom : "SQ";
                                uom = uom != "LBS" ? uom : "LB";
                                salesCat1 = _db.DSet.Tables["Item"].Rows[0][1].ToString().Trim();
                                salesCat2 = _db.DSet.Tables["Item"].Rows[0][2].ToString().Trim();
                                salesCat3 = _db.DSet.Tables["Item"].Rows[0][3].ToString().Trim();
                                salesCat4 = _db.DSet.Tables["Item"].Rows[0][4].ToString().Trim();
                                commodityCode = _db.DSet.Tables["Item"].Rows[0][5].ToString().Trim() != "" ? _db.DSet.Tables["Item"].Rows[0][5].ToString().Trim().PadLeft(3, '0') : " ";
                                lineType = _db.DSet.Tables["Item"].Rows[0][6].ToString().Trim();
                                stockingType = _db.DSet.Tables["Item"].Rows[0][7].ToString().Trim();
                                buyerNumber = _db.DSet.Tables["Item"].Rows[0][8].ToString().Trim();
                                buyerNumber = string.IsNullOrWhiteSpace(buyerNumber) || buyerNumber == "0" ? "110051" : buyerNumber;
                                leadTime = _db.DSet.Tables["Item"].Rows[0][9].ToString().Trim();
                                stockruncode = _db.DSet.Tables["Item"].Rows[0][10].ToString().Trim();
                                masterplanfamily = _db.DSet.Tables["Item"].Rows[0][11].ToString().Trim();
                                vendornum = _db.DSet.Tables["Item"].Rows[0][12].ToString().Trim();
                                if (lineType != "7" && lineType != "S")
                                {
                                    vendornum = " ";
                                }
                                else
                                {
                                    if (string.IsNullOrWhiteSpace(vendornum) || vendornum == "0")
                                    {
                                        vendornum = "850044";
                                    }
                                }
                                //   glClass = "MPSA";
                                glClass = "5SUB";
                                if (lineType == "7" || lineType == "S")
                                {
                                    //   glClass = "PPSA";
                                    glClass = "5PFG";
                                }
                                else if (lineType == "PA" && stockingType == "J")
                                {
                                    glClass = "5YLN";
                                }
                            }
                            break;
                        // For production mode set item values from the easyweb data
                        case "jde":
                            uom = orderItem.UOM;
                            uom = uom != "SQF" ? uom : "SQ";
                            uom = uom != "LBS" ? uom : "LB";
                            uom = string.IsNullOrWhiteSpace(uom) ? "EA" : uom;
                            salesCat1 = orderItem.SalesCat1.Length < 4 ? orderItem.SalesCat1 : orderItem.SalesCat1.Substring(0, 3);
                            salesCat2 = orderItem.SalesCat2.Length < 4 ? orderItem.SalesCat2 : orderItem.SalesCat2.Substring(0, 3);
                            salesCat3 = orderItem.SalesCat3.Length < 4 ? orderItem.SalesCat3 : orderItem.SalesCat3.Substring(0, 3);
                            salesCat4 = orderItem.SalesCat4.Length < 4 ? orderItem.SalesCat4 : orderItem.SalesCat4.Substring(0, 3);
                            commodityCode = !string.IsNullOrWhiteSpace(orderItem.CommodityCode) ? orderItem.CommodityCode : " ";
                            if (orderItem.StockingType == "M" && string.IsNullOrWhiteSpace(orderItem.LineType))
                            {
                                lineType = !string.IsNullOrWhiteSpace(orderItem.EasyWebLineType) ? orderItem.EasyWebLineType : "W";
                            }
                            else
                            {
                                lineType = orderItem.LineType;
                            }
                            stockingType = orderItem.StockingType;
                            buyerNumber = orderItem.BuyerNumber;
                            buyerNumber = string.IsNullOrWhiteSpace(buyerNumber) || buyerNumber == "0" ? "110051" : buyerNumber;
                            leadTime = orderItem.LeadTime;
                            stockruncode = orderItem.StockRunCode;
                            masterplanfamily = orderItem.MasterPlanFamily;
                            vendornum = orderItem.VendorNum;
                            if (lineType != "7" && lineType != "S")
                            {
                                vendornum = " ";
                            }
                            else
                            {
                                if (string.IsNullOrWhiteSpace(vendornum) || vendornum == "0")
                                {
                                    vendornum = "850044";
                                }
                            }
                            //    glClass = "MPSA";
                            glClass = "5SUB";
                            if (lineType == "7" || lineType == "S")
                            {
                                //  glClass = "PPSA";
                                glClass = "5PFG";
                            }
                            else if (lineType == "PA" && stockingType == "J")
                            {
                                glClass = "5YLN";
                            }
                            break;
                    }

                    // Check if UOM is missing and update the door item.
                    if (string.IsNullOrWhiteSpace(orderItem.UOM))
                    {
                        orderItem.UOM = uom;
                    }

                    // After the call to check for missing jde items get the short item number
                    try { orderItem.ShortItemNum = Convert.ToInt32(item.szIdentifierShortItem); }
                    catch { }
                    // After the call to check for missing jde items if there is an error then item is missing and will be added
                    if (!string.IsNullOrWhiteSpace(item.sz55ErrorDescription))
                    {
                        if (orderItem != null)
                        {
                            //if (!itemExclude.Any(x => orderItem.ItemNum.StartsWith(x)))
                            //{
                            itemUpdate = new ItemUpdate() { NewItemArray = new List<ItemAdd>() };
                            orderItem.Add = true;
                            itemUpdate.NewItemArray.Add(new ItemAdd()
                            {
                                Product_No = orderItem.ItemNum,
                                Search_Text = "",
                                Stocking_Type = stockingType,
                                G_L_Class = glClass,
                                Unit_of_Measure = uom,
                                Planner_Number_ALKY = " ",
                                Buyer_Number_ALKY = !string.IsNullOrWhiteSpace(buyerNumber) ? buyerNumber : " ",
                                //Buyer_Number_ALKY = "110025",
                                // Buyer_Number_ALKY = "800003",
                                Sales_Price_Level = "1",
                                //Item_Price_Group = "MULT532",
                                Item_Price_Group = " ",
                                //CD1
                                Sales_Catalog_Section = salesCat1,
                                //CD2
                                Sub_Section = salesCat2,
                                //CD3
                                Sales_Category_Code_3 = salesCat3 == "RHW" ? " " : salesCat3,
                                //CD4
                                Sales_Category_Code_4 = !string.IsNullOrWhiteSpace(salesCat4) ? salesCat4 : " ",
                                //PRP1
                                Commodity_Class = lineType == "7" || lineType == "S" ? commodityCode : " ",
                                // Master_Planning_Family = !string.IsNullOrWhiteSpace(masterplanfamily) && !(masterplanfamily.StartsWith("B")) ? masterplanfamily : "116",
                                Master_Planning_Family = !string.IsNullOrWhiteSpace(masterplanfamily) && !(masterplanfamily.StartsWith("B")) && masterplanfamily != "x" ? masterplanfamily : " ",
                                Warehouse_Process_Grp_1 = "1071",
                                //Category_Code_6 = "SCNPT",
                                //Category_Code_7 = "5",
                                Category_Code_6 = " ",
                                Category_Code_7 = " ",
                                Category_Code_8 = lineType == "S" ? "MISSTK" : "MISMIS",
                                Category_Code_9 = " ",
                                Planning_Code = "0",
                                Issue_Type_Code = "B",
                                Branch_Plant = "50000",
                                //Supplier_Number_ALKY = 120064,
                                //Supplier_Number_ALKY = linetype == "7" || linetype == "S" ? "120064" : " ",
                                Supplier_Number_ALKY = vendornum,
                                P4101_Version = "",
                                Description1 = orderItem.Description,
                                Description2 = "",
                                Leadtime_Level = !string.IsNullOrWhiteSpace(leadTime) ? Convert.ToInt32(leadTime) : 5,
                                Item_Pool_Code_PRP0 = "000500",
                                Line_Type_LNTY = lineType,
                                Inventory_Cost_Level_CLEV = "2",
                                Purchase_Price_Level_PPLV = "3",
                                Supplier_Rebate_Code_PRP3 = "RC",
                                //Category_Code_9_SRP9 = "AW"
                                Category_Code_9_SRP9 = !string.IsNullOrWhiteSpace(stockruncode) ? stockruncode : " ",
                                Location = lineType == "S" ? "SP.01.03.01.A" : " ",
                                Purchasing_Taxable = stockingType == "P" ? "Y" : ""
                            });
                            //}

                            // Call JDE to add the missing items
                            if (itemUpdate.NewItemArray.Count != 0)
                            {
                                json = JsonConvert.SerializeObject(itemUpdate);
                                result = SendWebApiMessage(apiUrl + "AddNewItemNumber", json).Result;
                                // After the api call to add the item check the return data for the short item number - if missing item was not added and send an email
                                if (!result.Contains("ShortItemNumber"))
                                {
                                    result1 = JsonConvert.DeserializeObject<Result>(result);
                                    SendMail("Stock code creation failure for EW Order" + EWOrderNum, "Stock code: " + orderItem.ItemNum + "\r\nError: " + result1.message, emailIT);
                                }
                                else
                                {
                                    // Add labour cost for OP parts
                                    if (orderItem.ItemNum.EndsWith("OP10") || orderItem.ItemNum.EndsWith("OP20") || orderItem.ItemNum.EndsWith("OP30"))
                                    {
                                        json = GetItemCostJson(orderItem);
                                        if (json != "")
                                        {
                                            result = SendWebApiMessage(apiUrl + "N554105Z", json).Result;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Process the UOM conversion and cost roll and freeze for all items with a line type of 7 or P
            UnitOfMesaure unit;
            CostRollAndFreeze crf = new CostRollAndFreeze() { ItemNbrArray = new List<CRFDetail>() };
            foreach (var item in items.N554130_Repeating)
            {
                orderItem = doorItem.Items.Where(x => x.ItemNum == item.szIdentifier2ndItem && item.szIdentifier2ndItem != discountItemNum).FirstOrDefault();
                if (orderItem != null)
                {
                    if (orderItem.Add == true) // Check if item has been added as a new item
                    {
                        if (orderItem.Weight != "" && orderItem.Weight != "0" && orderItem.UOM.StartsWith("EA") && (orderItem.LineType == "7" || orderItem.StockingType == "P"))
                        {
                            unit = new UnitOfMesaure() { Item_Number = orderItem.ItemNum, GridData_1 = new List<UOMDetail>() };
                            unit.GridData_1.Add(new UOMDetail() { From_UoM = orderItem.UOM, Quantity = orderItem.Weight.ToString(), To_UoM = "LB" });
                            json = JsonConvert.SerializeObject(unit);
                            result = SendWebApiMessage(apiUrl + "ItemUnitOfMeasureConversion", json).Result;
                        }
                        if (orderItem.StockingType == "M") // The initial check was for StockingType = P and has been changed on 5-11-2022
                        {
                            crf.ItemNbrArray.Add(new CRFDetail() { Item_Nbr = orderItem.ItemNum });
                        }
                    }
                }
            }
            if (crf.ItemNbrArray.Count > 0)
            {
                json = JsonConvert.SerializeObject(crf);
                // Cost roll and freeze commented on 5-17-2022
                //result = SendWebApiMessage(apiUrl + "CostRollandFreeze", json).Result;
            }
        }
        return items;
    }

    // Get the json for adding the stockcode cost
    private static string GetItemCostJson(OrderItem orderItem)
    {
        string json = "";
        DateTime dtNow = DateTime.Now;
        string date = dtNow.Month.ToString() + "/" + dtNow.Day.ToString() + "/" + dtNow.Year.ToString();
        int time = Convert.ToInt32(DateTime.Now.ToString("HHmmss"));
        string itemcost;
        string ledgtype = "08";
        string costingselP = " ";
        string costingselI = "I";
        itemcost = orderItem.ItemCost;
        ItemCost itemCost = new ItemCost() { F4105Z1 = new List<ItemCostDetail>() };
        itemCost.F4105Z1.Add(new ItemCostDetail()
        {
            szEdiUserId = "DEV13",
            //szEdiBatchNumber = "101",
            //changed on Feb 23 2021
            //if one item then batch number has to be unique but array of multiple items can go under one batch
            szEdiBatchNumber = orderItem.ItemNum.Length > 15 ? orderItem.ItemNum.Substring(0, 15) : orderItem.ItemNum,
            szEdiTransactNumber = "1",
            mnEdiLineNumber = 1,
            cDirectionIndicator = "1",
            cEdiSuccessfullyProcess = " ",
            szTransactionAction = "A",
            szIdentifier2ndItem = orderItem.ItemNum,
            szCostCenter = "50000",
            szLedgType = ledgtype,
            mnAmountUnitCost = Math.Round(Convert.ToDecimal(itemcost.Trim()), 4),
            cCostingSelectionPurchasi = costingselP,
            cCostingSelectionInventor = costingselI,
            szUserId = "DEV13",
            szProgramId = "RWLoad",
            szWorkStationId = "D1",
            jdDateUpdated = date,
            mnTimeOfDay = time,
            szErrorDescription = " "
        });
        json = JsonConvert.SerializeObject(itemCost);
        return json;
    }

    // Process BOM and Routing
    private void AddBomAndRouting(EWOrder ewOrder, string apiUrl, string apiUrl2)
    {
        string bomItem;
        string result;
        int bomCount1;
        int bomCount2;
        ItemQuery itemQuery;
        ItemQueryReturn items;
        OrderItem orderItem1;
        OrderItem orderItem2;
        OrderItem orderItem3;
        // Get parts to exclude from bom and routing from the configuration file
        string[] pctExclude = _configuration.GetValue<string>("AppSettings:PctPartExclude").Split(',');
        // For development 
        if (_environment == "dev")
        {
            foreach (var doorItem in ewOrder.DoorItems)
            {
                foreach (var item in doorItem.Items)
                {
                    // Exclude items with no item number, cut instructions and items excluded in the configuration file
                    if (!string.IsNullOrWhiteSpace(item.ItemNum) && !item.ItemNum.StartsWith("Cut To Instruction") && !item.ItemNum.EndsWith("-CUT") && !pctExclude.Any(x => item.ItemNum.StartsWith(x)))
                    {
                        bomCount1 = _syspro.GetTable("select Component,QtyPer from BomStructure where ParentPart like '" + item.ItemNum + "'", "Bom"); // Select bom details for parent item
                        if (bomCount1 > 0)
                        {
                            // Add first level bom items to JDE
                            AddBom("Bom", null, item.ItemNum, apiUrl);
                            if (item.RoutingTextList != null) // If item has routing details add to JDE
                            {
                                AddRouting(item, doorItem, apiUrl, apiUrl2);
                            }
                            // Add second level bom items to JDE
                            for (int i = 0; i < bomCount1; i++)
                            {
                                bomItem = _syspro.DSet.Tables["Bom"].Rows[i][0].ToString().Trim();
                                bomCount2 = _syspro.GetTable("select Component,QtyPer from BomStructure where ParentPart like '" + bomItem + "'", "Bom2"); // Select bom details for components
                                if (bomCount2 > 0)
                                {
                                    // Add second level bom items to JDE
                                    AddBom("Bom2", null, bomItem, apiUrl);
                                    orderItem1 = doorItem.Items.Where(x => x.ItemNum == bomItem).FirstOrDefault();
                                    if (orderItem1 == null)
                                    {
                                        itemQuery = new ItemQuery() { ItemArray = new List<Item>(), webOrderNbr = "", configurationNbr = 0 };
                                        itemQuery.ItemArray.Add(new Item { szIdentifier2ndItem = bomItem });
                                        string json = JsonConvert.SerializeObject(itemQuery);
                                        // Call JDE to get item data
                                        result = SendWebApiMessage(apiUrl + "N554130", json).Result;
                                        if (result != null)
                                        {
                                            items = JsonConvert.DeserializeObject<ItemQueryReturn>(result);
                                            orderItem1 = new OrderItem { ItemNum = items.N554130_Repeating[0].szIdentifier2ndItem, ShortItemNum = Convert.ToInt32(items.N554130_Repeating[0].szIdentifierShortItem) };
                                        }
                                        else
                                        {
                                            orderItem1 = new OrderItem { ItemNum = bomItem, ShortItemNum = 0 };
                                        }
                                    }
                                    if (orderItem1.RoutingTextList != null)
                                    {
                                        AddRouting(item, doorItem, apiUrl, apiUrl2);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        else
        {
            // Add bom details for production 4/6/2022
            foreach (var doorItem in ewOrder.DoorItems)
            {
                foreach (var item1 in doorItem.Items)
                {
                    if (item1.BOMs != null)
                    {
                        if (item1.BOMs.Count > 0)
                        {
                            // Exclude items with no item number, cut instructions and items excluded in the configuration file
                            if (!item1.ItemNum.StartsWith("Cut To Instruction") && !item1.ItemNum.EndsWith("-CUT") && !pctExclude.Any(x => item1.ItemNum.StartsWith(x)))
                            {
                                orderItem1 = doorItem.Items.Where(x => x.ItemNum == item1.ItemNum).FirstOrDefault();
                                if (!item1.ItemNum.StartsWith("Cut To Instruction") && !item1.ItemNum.EndsWith("-CUT") && !pctExclude.Any(x => item1.ItemNum.StartsWith(x)) && item1.LineType != "Y")
                                {
                                    // Add first level bom items to JDE
                                    //if (orderItem1.Add == true)
                                    {
                                        AddBom("", orderItem1, item1.ItemNum, apiUrl);
                                    }
                                }
                                // Add second level bom items to JDE
                                foreach (var item2 in item1.BOMs)
                                {
                                    orderItem2 = doorItem.Items.Where(x => x.ItemNum == item2.ItemNum).FirstOrDefault();
                                    if (orderItem2 == null)
                                    {
                                        itemQuery = new ItemQuery() { ItemArray = new List<Item>(), webOrderNbr = "", configurationNbr = 0 };
                                        itemQuery.ItemArray.Add(new Item { szIdentifier2ndItem = item2.ItemNum });
                                        string json = JsonConvert.SerializeObject(itemQuery);
                                        // Call JDE to get item data
                                        result = SendWebApiMessage(apiUrl + "N554130", json).Result;
                                        if (result != null)
                                        {
                                            items = JsonConvert.DeserializeObject<ItemQueryReturn>(result);
                                            orderItem2 = new OrderItem { ItemNum = items.N554130_Repeating[0].szIdentifier2ndItem, ShortItemNum = Convert.ToInt32(items.N554130_Repeating[0].szIdentifierShortItem) };
                                        }
                                        else
                                        {
                                            orderItem2 = new OrderItem { ItemNum = item2.ItemNum, ShortItemNum = 0 };
                                        }
                                    }
                                    if (!item2.ItemNum.StartsWith("Cut To Instruction") && !item2.ItemNum.EndsWith("-CUT") && !pctExclude.Any(x => item2.ItemNum.StartsWith(x)))
                                    {
                                        //if (orderItem2.Add == true)
                                        {
                                            AddBom("", orderItem2, item2.ItemNum, apiUrl);
                                        }
                                    }
                                    if (item2.BOMs != null)
                                    {
                                        if (item2.BOMs.Count > 0)
                                        {
                                            // Add third level bom items to JDE
                                            foreach (var item3 in item2.BOMs)
                                            {
                                                orderItem3 = doorItem.Items.Where(x => x.ItemNum == item3.ItemNum).FirstOrDefault();
                                                if (orderItem3 == null)
                                                {
                                                    itemQuery = new ItemQuery() { ItemArray = new List<Item>(), webOrderNbr = "", configurationNbr = 0 };
                                                    itemQuery.ItemArray.Add(new Item { szIdentifier2ndItem = item3.ItemNum });
                                                    string json = JsonConvert.SerializeObject(itemQuery);
                                                    // Call JDE to get item data
                                                    result = SendWebApiMessage(apiUrl + "N554130", json).Result;
                                                    if (result != null)
                                                    {
                                                        items = JsonConvert.DeserializeObject<ItemQueryReturn>(result);
                                                        orderItem3 = new OrderItem { ItemNum = items.N554130_Repeating[0].szIdentifier2ndItem, ShortItemNum = Convert.ToInt32(items.N554130_Repeating[0].szIdentifierShortItem) };
                                                    }
                                                    else
                                                    {
                                                        orderItem3 = new OrderItem { ItemNum = item2.ItemNum, ShortItemNum = 0 };
                                                    }
                                                }
                                                if (!item3.ItemNum.StartsWith("Cut To Instruction") && !item3.ItemNum.EndsWith("-CUT") && !pctExclude.Any(x => item3.ItemNum.StartsWith(x)))
                                                {
                                                    if (orderItem3.Add == true)
                                                    {
                                                        AddBom("", orderItem3, item3.ItemNum, apiUrl);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // Add routing detail 4/6/2022
            foreach (var doorItem in ewOrder.DoorItems)
            {
                foreach (var item in doorItem.Items)
                {
                    if (item.RoutingTextList != null)
                    //if (orderItem.Add == true)
                    {
                        AddRouting(item, doorItem, apiUrl, apiUrl2);
                    }
                }
            }
        }
    }

    // Process BOM
    private string AddBom(string table, OrderItem orderItem, string stockcode, string apiUrl)
    {
        string result = "";
        string bomItem;
        string bomQuantity;
        List<string> bomItems = new List<string>();
        int jdeCount;
        if (!orderItem.ItemNum.EndsWith("OP10"))
        {
            BillOfMaterial bom = new BillOfMaterial() { GridData_1 = new List<GridData>(), Parent_ItemNbr = stockcode, Parent_Branch = "50000" };
            switch (_environment)
            {
                case "dev":
                    int bomCount = _syspro.DSet.Tables[table].Rows.Count;
                    for (int i = 0; i < bomCount; i++)
                    {
                        bomItem = _syspro.DSet.Tables[table].Rows[i][0].ToString().Trim();
                        bomQuantity = _syspro.DSet.Tables[table].Rows[i][1].ToString();
                        bom.GridData_1.Add(new GridData()
                        {
                            Quantity = bomQuantity,
                            Item_NumberG = bomItem
                        });
                    }
                    break;
                case "jde":
                    if (orderItem.BOMs != null)
                    {
                        foreach (var item in orderItem.BOMs)
                        {
                            if (!item.ItemNum.StartsWith("Cut") && !item.ItemNum.EndsWith("-CUT") && !item.ItemNum.EndsWith("OP10"))
                            {
                                bomItem = item.ItemNum;
                                bomQuantity = item.Quantity.ToString();
                                bom.GridData_1.Add(new GridData()
                                {
                                    Quantity = bomQuantity,
                                    Item_NumberG = bomItem
                                });

                                // Check Jde for missing bom items and send email - disabled on July 7, 2022
                                //jdeCount = _jde.GetTable("SELECT IXKIT,IXKITL,IXAITM,IXQNTY FROM CRPDTA.F3002 where IXMMCU = '       50000' and IXKITL ='" + stockcode + "' and IXAITM = '" + bomItem + "'", "jdeBom");
                                //if (jdeCount == 0)
                                //{
                                //    bomItems.Add(bomItem);
                                //}
                            }
                        }
                        // Send email for missing bom items
                        if (bomItems.Count > 0)
                        {
                            SendMail("Missing BOM items for EW Order " + EWOrderNum, "Parent stock code: " + bom.Parent_ItemNbr + "<br/>BOM stock codes " + string.Join(", ", bomItems.ToArray()), emailJdeBom);
                        }
                    }
                    break;
            }

            if (bom.GridData_1.Count > 0)
            {
                string json = JsonConvert.SerializeObject(bom);
                result = SendWebApiMessage(apiUrl + "AddNewBillofMaterial", json).Result;
                if (!result.Contains("ERROR"))
                {
                    string bomitems = "";
                    foreach (var item in bom.GridData_1)
                    {
                        bomitems += !string.IsNullOrEmpty(bomitems) ? ", " + item.Item_NumberG : item.Item_NumberG;
                    }
                    // Check Jde to get bom items and if not there or a different count send email
                    jdeCount = _jde.GetTable("SELECT IXKIT,IXKITL,IXAITM,IXQNTY FROM CRPDTA.F3002 where IXMMCU = '       50000' and IXKITL ='" + bom.Parent_ItemNbr + "'", "jdeBom");
                    if (jdeCount != bom.GridData_1.Count)
                    {
                        SendMail("BOM creation failure for EW Order " + EWOrderNum, "Parent stock code: " + bom.Parent_ItemNbr + "<br/>BOM stock codes " + bomitems, emailJdeBom);
                    }
                }
            }
        }
        return result;
    }

    // Process Routing
    private string AddRouting(OrderItem orderItem, DoorItem doorItem, string apiUrl, string apiUrl2)
    {
        string result = "";
        string json;
        string inputtext;
        string date;
        string[] mokey;
        string[] workCenter;
        string[] operSeq;
        string[] runLabor;
        double runTime;
        bool V2Flag = false;
        List<RoutingText> routingTexts = orderItem.RoutingTextList;
        List<JDERouting> routings = new List<JDERouting>();
        List<JDERoutingV2> routingsV2 = new List<JDERoutingV2>();
        JDERouting routing = null;
        JDERoutingV2 routingV2 = null;
        OrderItem orderItem1;
        OrderItem orderItem2;
        if (!orderItem.ItemNum.EndsWith("OP10"))
        {
            if (orderItem.BOMs.Where(x => x.ItemNum.EndsWith("OP10")).FirstOrDefault() == null)
            {
                routing = new JDERouting() { Branch = "50000", Item_Number = orderItem.ItemNum, RoutingDetail = new List<RoutingDetail>(), P3003_Version = "" };
            }
            else
            {
                routingV2 = new JDERoutingV2() { Branch = "50000", Item_Number = orderItem.ItemNum, RoutingDetail = new List<RoutingDetailV2>(), P3003_Version = "" };
                V2Flag = true;
            }
            switch (_environment)
            {
                case "dev":
                    int dataRows = _db.GetTable("SELECT Work_Center,Oper_Seq,Run_Labor FROM dbo.SysproToJdeItemMaster WHERE stkCode='" + orderItem.ItemNum + "' AND partCategory='M'", "Item"); // Get routing data
                    if (dataRows > 0)
                    {
                        workCenter = _db.DSet.Tables["Item"].Rows[0][0].ToString().Split(',');
                        operSeq = _db.DSet.Tables["Item"].Rows[0][1].ToString().Split(',');
                        runLabor = _db.DSet.Tables["Item"].Rows[0][2].ToString().Split(',');
                        for (int i2 = 0; i2 < workCenter.Length; i2++)
                        {
                            if (!string.IsNullOrWhiteSpace(workCenter[i2]) && !string.IsNullOrWhiteSpace(operSeq[i2]) && !string.IsNullOrWhiteSpace(runLabor[i2]))
                            {
                                if (operSeq[i2] == "0") operSeq[i2] = "10";
                                routing.RoutingDetail.Add(new RoutingDetail() { Work_Center = workCenter[i2], Oper_Seq = operSeq[i2], Run_Labor = (Convert.ToDecimal(runLabor[i2]) * 1000).ToString() });
                                routings.Add(routing);
                            }
                        }
                    }
                    break;
                case "jde":
                    foreach (var routingText in routingTexts)
                    {
                        foreach (var operation in routingText.Operations)
                        {
                            try
                            {
                                runTime = 0;
                                if (orderItem.ItemNum.StartsWith("C") && orderItem.ItemNum.Substring(3, 1) == "-" && (operation.WorkCenter == "50-35-001" || operation.WorkCenter == "50-35-002"))
                                {
                                    orderItem1 = doorItem.Items.Where(x => x.ItemNum == orderItem.ItemNum).FirstOrDefault();
                                    foreach (var item in orderItem1.BOMs)
                                    {
                                        orderItem2 = doorItem.Items.Where(x => x.ItemNum == item.ItemNum).FirstOrDefault();
                                        if (orderItem2.ComRunTime != "" && orderItem2.ComRunTime != "0")
                                        {
                                            runTime += Convert.ToDouble(orderItem2.ComRunTime) * orderItem2.Quantity;
                                        }
                                    }
                                }
                                if (!V2Flag)
                                {
                                    routing.RoutingDetail.Add(new RoutingDetail()
                                    {
                                        Work_Center = operation.WorkCenter,
                                        Oper_Seq = operation.SequenceNumber,
                                        Run_Labor = runTime == 0 ? (Convert.ToDecimal(operation.RunLabor) * 1000).ToString() : (Convert.ToDecimal(runTime) * 1000).ToString()
                                    });
                                    routings.Add(routing);
                                }
                                else
                                {
                                    routingV2.RoutingDetail.Add(new RoutingDetailV2()
                                    {
                                        Work_Center = operation.WorkCenter,
                                        Oper_Seq = operation.SequenceNumber,
                                        Run_Labor = runTime == 0 ? (Convert.ToDecimal(operation.RunLabor) * 1000).ToString() : (Convert.ToDecimal(runTime) * 1000).ToString(),
                                        Supplier = !string.IsNullOrWhiteSpace(orderItem.Supplier) ? Convert.ToInt32(orderItem.Supplier) : 0,
                                        Cost_Type = "D1",
                                        PO_YN = "Y",
                                        Time_Basis = "U"
                                    });
                                    routingsV2.Add(routingV2);
                                }
                            }
                            catch
                            {
                                WriteLog("Error adding routing for " + routing.Item_Number + ": Work_Center - " + operation.WorkCenter + ", Oper_Seq - " + operation.SequenceNumber + ", Run_Labor - " + operation.RunLabor);
                            }
                        }
                    }
                    break;
            }
            if (routings.Count > 0 || routingsV2.Count > 0)
            {
                if (!V2Flag)
                {
                    foreach (var item in routings)
                    {
                        // Call to add routing
                        json = JsonConvert.SerializeObject(item);
                        result = SendWebApiMessage(apiUrl + "AddNewRoutingV2", json).Result;
                    }
                }
                else
                {
                    foreach (var item in routingsV2)
                    {
                        // Call to add routing
                        json = JsonConvert.SerializeObject(item);
                        result = SendWebApiMessage(apiUrl + "AddNewRoutingOutsideOper", json).Result;
                    }
                }
            }

            // Add media attachment for the parent stockcode
            if (orderItem.ShortItemNum != 0)
            {
                date = DateTime.Now.ToString("yyyyMMdd");
                mokey = new string[] { orderItem.ShortItemNum.ToString(), "50000", "M", "0.000", "120.00", "", "", date };
                inputtext = "";
                foreach (var routingText in routingTexts)
                {
                    foreach (var operation in routingText.Operations)
                    {
                        if (inputtext != "") inputtext += "<BR>";
                        inputtext += operation.Description;
                    }

                    TextAttachment textAttachment = new TextAttachment()
                    {
                        moStructure = "GT3003B",
                        moKey = mokey,
                        formName = "",
                        version = "",
                        itemName = "Routing Text",
                        inputText = inputtext
                    };
                    json = JsonConvert.SerializeObject(textAttachment);
                    result = SendWebApiMessage(apiUrl2 + "addtext", json).Result;
                }
            }
        }
        return result;
    }

    // Process order details and header
    private string AddOrderAndDetail(EWOrder ewOrder, OrderInfo orderInfo, string apiUrl)
    {
        string returnValue = "";
        string json;
        DateTime dtNow = DateTime.Now;
        string date = dtNow.Month.ToString() + "/" + dtNow.Day.ToString() + "/" + dtNow.Year.ToString();
        dtNow = dtNow.AddDays(15);
        string defaultshipdate = dtNow.Month.ToString() + "/" + dtNow.Day.ToString() + "/" + dtNow.Year.ToString();
        string time = DateTime.Now.ToString("HHmmss");
        string catWorkOrder; // Not used
        List<string> sbGlazing = new List<string>() { "A", "J", "L", "M", "S", "X" };
        string[] pctExclude = _configuration.GetValue<string>("AppSettings:PctPartExclude").Split(',');
        OrderDetailReturn detailReturn;
        JsonSerializerSettings settings = new JsonSerializerSettings
        {
            DateFormatString = "M/d/yyyy"
        };
        OrderDetail orderDetail = new OrderDetail() { F47012 = new List<DetailLine>() };

        PriceUpdate priceUpdate = new PriceUpdate() { PriceArray = new List<PricetItem>() };

        /* -//ys-wkg
        
        string strOrderStatus = "";
        string strReasonCodes = "";
        string strOrderStatusPre = "";

        strOrderStatusPre = _database.GetField("select  DESIGN_INPUT_VAL from CO_DES_INPUT where DESIGN_ID='" + order1.Detail[0].ID + "' and INPUT_NAME = 'GL_ORDER_STATUS1'");
        strReasonCodes = _database.GetField("select  DESIGN_INPUT_VAL from CO_DES_INPUT where DESIGN_ID='" + order1.Detail[0].ID + "' and INPUT_NAME = 'GL_REASON_CODES1'");

        strOrderStatus = strOrderStatusPre == "" ? "SO" : strOrderStatusPre;
        

        if (strOrderStatus == "SO")
        {
            strReasonCodes = " ";
        }
        *///endys

        // Format ship date for jde order
        if (!string.IsNullOrWhiteSpace(ewOrder.ShipDate))
        {
            DateTime dt = Convert.ToDateTime(ewOrder.ShipDate);
            ewOrder.ShipDate = dt.Month + "/" + dt.Day + "/" + dt.Year;
        }
        else
        {
            ewOrder.ShipDate = defaultshipdate;
        }

        foreach (var doorItem in ewOrder.DoorItems)
        {
            doorItem.OrderInfo = orderInfo;
            string pricePerUnit;
            foreach (var item in doorItem.Items)
            {
                item.AddItemDetail = true;
                if (!item.ItemNum.StartsWith("Cut To Instruction") && !item.ItemNum.EndsWith("-CUT") && !pctExclude.Any(x => item.ItemNum.StartsWith(x)))
                {
                    // Exclude items for Milestone
                    if (ewOrder.YLineItem == "MILESTONE" && (item.ItemNum.ToLower().StartsWith("options ") || item.ItemNum.StartsWith("SINS") || item.ItemNum.StartsWith("SDSB")))
                    {
                        item.AddItemDetail = false;
                    }
                    // Compute item price per unit
                    pricePerUnit = item.Discount != 0 ? (Math.Round(item.UnitPrice,2) - Math.Round((Math.Round(item.UnitPrice,2) * (item.Discount / 100)), 2)).ToString() : item.UnitPrice.ToString();
                    // Set category work order field when item num has the correct format
                    catWorkOrder = " ";
                    //if (((item.ItemNum.StartsWith("3G") || item.ItemNum.StartsWith("4")) && Char.IsLetter(Convert.ToChar(item.ItemNum.Substring(2, 1))) || (item.ItemNum.StartsWith("55")) || item.ItemNum.StartsWith("ZZCUTLITE")))
                    if ((item.ItemNum.StartsWith("4C") || item.ItemNum.StartsWith("ZZCUTLITE")))
                    {
                        catWorkOrder = "WWO";
                        if (doorItem.Exclude)
                        {
                            ewOrder.ExcludedItem.Add(item.ItemNum + "~" + item.Quantity.ToString());
                            item.AddItemDetail = false;
                        }
                    }
                    if (item.LineNum != 0 && item.AddItemDetail)
                    {
                        // Add item detail lines for JDE
                        orderDetail.F47012.Add(new DetailLine()
                        {
                            cEDIType = "2",
                            szCompanyKeyEdiOrder = "00500",
                            //Mnedidocument number is changed for 2nd door ***  Anil
                            mnEdiDocumentNumber = doorItem.OrderInfo.mnEdiDocumentNumber,
                            szEdiDocumentType = "SZ",
                            mnEdiLineNumber = item.LineNum.ToString(),
                            szEdiTransactionSet = "850",
                            jdEdiTransmissionDate = date,
                            cEdiSendRcvIndicator = "R",
                            cEdiSuccessfullyProcess = "2",
                            szEdiBatchNumber = doorItem.OrderInfo.szEdiBatchNumber,
                            szCompanyKeyOrderNo = "00500",
                            mnDocumentOrderInvoiceE = doorItem.OrderInfo.mnDocumentOrderInvoiceE,
                            // szOrderType = "SO", //org

                            szOrderType = strOrderStatus,  //SO or N1 - ys
                            //       mnCarrier = "0",
                            mnLineNumber = item.LineNum.ToString(),
                            szCostCenter = "50000",
                            szCompany = "00500",
                            mnAddressNumber = ewOrder.CustomerCode,
                            //mnAddressNumber = "166",
                            //mnAddressNumberShipTo = "1164",
                            mnAddressNumberShipTo = ewOrder.ShipTo,
                            mnAddressNumberParent = ewOrder.CustomerCode,
                            //mnAddressNumberParent = "166",
                            jdDateRequestedJulian = ewOrder.ShipDate,
                            jdDateTransactionJulian = date,
                            szReference1 = ewOrder.CustomerPo,
                            szIdentifier2ndItem = item.ItemNum,
                            szLot = item.LotNumber,
                            szDescriptionLine1 = item.Description,
                            szDescriptionLine2 = " ",
                            //szLineType = item.LineType,
                            szLineType = " ", // Line tpye will be blank for the line type will get its value from the jde item master - 4/10/2022
                            //mnLineNumberKitMaster = item.RefLineNum != 0 ? item.RefLineNum.ToString() + ".000" : "1",
                            //mnComponentNumber = item.RefLineNum != 0 ? item.RefLineNum.ToString() + ".0" : "1",
                            //mnRelatedKitComponent = item.RefLineNum != 0 ? item.RefLineNum.ToString() : "1",
                            mnLineNumberKitMaster = "0",
                            mnComponentNumber = "0",
                            mnRelatedKitComponent = "0",
                            mnUnitsTransactionQty = item.Quantity.ToString(),
                            //mnAmtListPricePerUnit = ewOrder.Country.ToLower() == "ca" ? item.UnitPrice.ToString() : "0",
                            mnAmtListPricePerUnit = ewOrder.Country.ToLower() == "ca" ? pricePerUnit : "0",
                            mnAmountExtendedPrice = ewOrder.Country.ToLower() == "ca" ? Math.Round(((item.UnitPrice - (item.UnitPrice * (item.Discount / 100))) * (double)item.Quantity), 2).ToString() : "0",
                            mnAmtPricePerUnit2 = ewOrder.Country.ToLower() == "ca" ? pricePerUnit : "0",
                            mnAmtForPricePerUnit = ewOrder.Country.ToLower() != "ca" ? pricePerUnit : "0",
                            //mnAmountListPriceForeign = ewOrder.Country.ToLower() != "ca" ? item.UnitPrice.ToString() : "0",
                            mnAmountListPriceForeign = ewOrder.Country.ToLower() != "ca" ? pricePerUnit : "0",
                            mnAmountForeignExtPrice = ewOrder.Country.ToLower() != "ca" ? Math.Round(((item.UnitPrice - (item.UnitPrice * (item.Discount / 100))) * (double)item.Quantity), 2).ToString() : "0",
                            mnAmountForeignExtCost = "0",
                            mnAmountForeignUnitCost = "0",
                            mnCurrencyConverRateOv = "0",
                            //mnCurrencyConverRateOv = ewOrder.Country.ToLower() != "ca" ? "1.0" : "0",
                            szCurrencyCodeFrom = " ",
                            //szCurrencyCodeFrom = ewOrder.Country.ToLower() != "ca" ? "USD" : " ",
                            mnAmountUnitWeight = item.Weight != "" ? (Convert.ToDouble(item.Weight) * item.Quantity).ToString() : "0",
                            cPriceOverrideCode = "1",
                            cTemporaryPriceYN = "N",
                            cTaxableYN = ewOrder.Country == "US" ? "N" : "Y",
                            // szRouteCode = "514",
                            szRouteCode = " ",
                            //  szFreightHandlingCode = "R",
                            //   mnCarrier = "120015",
                            szFreightHandlingCode = " ",
                            mnCarrier = "0",
                            szModeOfTransport = "TL",
                            mnCentury = "20",
                            cWoOrderFreezeCode = "N",
                            mnUserReservedAmount = "0",
                            szTransactionOriginator = "W_ORDER",
                            szProgramId = "WEBORDER",
                            szUserId = "WEBORDER",
                            jdDateUpdated = date,
                            mnTimeOfDay = time,
                            nSourceOfOrder = "1",
                            szIntegrationReference01 = ewOrder.SerialNum,
                            szIntegrationReference02 = ewOrder.ConfigReference,
                            szIntegrationReference03 = item.LineType2,
                            //szIntegrationReference04 = " ",
                            szIntegrationReference04 = ewOrder.SysproSalesOrder.StartsWith("5") ? ewOrder.SysproSalesOrder : " ",
                            szIntegrationReference05 = ewOrder.SysproSalesOrder.StartsWith("5") ? "No PO" : " ",
                            szReference2Vendor = doorItem.OrderTag,
                            //cWOItmNbr = catWorkOrder == "WWO" ? "Y" : " ",
                            //szCategoriesWorkOrder001 = catWorkOrder,
                            cWOItmNbr = catWorkOrder = " ",
                            szCategoriesWorkOrder001 = " ",
                            szCategoriesWorkOrder002 = " ",
                            szCategoriesWorkOrder003 = " ",
                            szCategoriesWorkOrder004 = " ",
                            szCategoriesWorkOrder005 = " ",
                            szCategoriesWorkOrder006 = " ",
                            szCategoriesWorkOrder007 = " ",
                            szCategoriesWorkOrder008 = " ",
                            szCategoriesWorkOrder009 = " ",
                            szCategoriesWorkOrder010 = " ",
                            szProgramId_2 = " ",
                            mnTimeOfDay_2 = 0,
                            mnUserReservedNumber = 0,
                            mnNumericField01 = item.ItemNum.StartsWith("L") && sbGlazing.Contains(item.ItemNum.Substring(1, 1)) ? doorItem.WindowQuantity : 0,
                            mnNumericField02 = 0,
                            mnNumericField03 = 0,
                            mnNumericField04 = 0,
                            mnNumericField05 = 0,
                            szStringField01 = " ",
                            szStringField02 = " ",
                            szStringField03 = " ",
                            szStringField04 = " ",
                            szStringField05 = " ",
                            szErrorDescription = " "
                            //Szir05 = "No PO"
                        });
                    }
                }
            }

            // Add price and discount items for JDE
            // Price adjustment end point"http://jdeaisdv/jderest/orchestrator/N554074A"  

            double pricePerUnit2;
            foreach (var item in doorItem.Items)
            {
                // Testing only
                if (item.ItemNum == "PCT2H-D108000902-SBTVL")
                {
                    string s = "";
                }
                // Add item pricing to JDE
                if (!string.IsNullOrWhiteSpace(item.ItemNum) && item.ItemNum != "Freight" && item.UnitPrice != 0 && item.ShortItemNum > 0 && !item.ItemNum.StartsWith("Cut To Instruction") && !item.ItemNum.EndsWith("-CUT") && !pctExclude.Any(x => item.ItemNum.StartsWith(x)) && item.AddItemDetail)
                {
                    priceUpdate.PriceArray.Add(new RaynorJdeApi.Models.PricetItem()
                    {
                        mnDocumentOrderInvoiceE = Convert.ToInt32(doorItem.OrderInfo.mnDocumentOrderInvoiceE),
                        // szOrderType = "SO", //org
                        szOrderType = strOrderStatus,  //SO or N1 -ys

                        szCompanyKeyOrderNo = "00500",
                        mnLineNumber = item.LineNum,
                        mnSequenceNumber = 0,
                        szPriceAdjustmentScheduleN = "RWEW",
                        szPriceAdjustmentType = " ",
                        mnIdentifierShortItem = item.ShortItemNum,
                        mnAddressNumber = 0,
                        szCurrencyCodeFrom = ewOrder.Country.ToLower() == "ca" ? "CAD" : "USD",
                        szUnitOfMeasureAsInput = item.UOM,
                        mnQuantityMinimum = 0,
                        cBasisCode = " ",
                        mnFactorValue = item.Quantity.ToString(),
                        cAdjustmentBasedon = " ",
                        mnAmtForPricePerUnit = ewOrder.Country.ToLower() != "ca" ? Math.Round(item.UnitPrice, 4) : 0,
                        mnAmtPricePerUnit2 = ewOrder.Country.ToLower() == "ca" ? Math.Round(item.UnitPrice, 4) : 0,
                        szGlClass = " ",
                        szAdjustmentReasonCode = " ",
                        cAdjustmentControlCode = " ",
                        cManualDiscount = " ",
                        cPriceOverrideCode = "1",
                        cOrderLevelAdjustmentYN = " ",
                        cMutuallyExclusiveAdjustme = "0",
                        cPromotionDisplayControl = "0",
                        szUserId = ewOrder.UserId,
                        szProgramId = "RWWEB",
                        szWorkStationId = "P1",
                        jdDateUpdated = date,
                        mnTimeOfDay = Convert.ToInt32(time),
                        cNewBasePriceFlag = " ",
                        szDescriptionLine1 = "Base Price",
                        szErrorDescription = " ",
                        mnTier = 0,
                    });
                    if (item.Discount != 0)
                    {
                        pricePerUnit2 = item.Discount != 0 ? -(Math.Round((item.UnitPrice * (item.Discount / 100)), 4)) : 0;
                        priceUpdate.PriceArray.Add(new RaynorJdeApi.Models.PricetItem()
                        {
                            mnDocumentOrderInvoiceE = Convert.ToInt32(doorItem.OrderInfo.mnDocumentOrderInvoiceE),
                            // szOrderType = "SO",

                            szOrderType = strOrderStatus,  //SO or N1  - ys
                            szCompanyKeyOrderNo = "00500",
                            mnLineNumber = item.LineNum,
                            mnSequenceNumber = 50,
                            szPriceAdjustmentScheduleN = " ",
                            szPriceAdjustmentType = "RWDISC",
                            mnIdentifierShortItem = item.ShortItemNum,
                            mnAddressNumber = 10,
                            szCurrencyCodeFrom = ewOrder.Country.ToLower() == "ca" ? "CAD" : "USD",
                            szUnitOfMeasureAsInput = item.UOM,
                            mnQuantityMinimum = 0,
                            cBasisCode = "2",
                            mnFactorValue = item.Discount != 0 ? (item.Discount * -1).ToString() : "0",
                            cAdjustmentBasedon = "N",
                            mnAmtForPricePerUnit = ewOrder.Country.ToLower() != "ca" ? Math.Round(pricePerUnit2, 4) : 0,
                            mnAmtPricePerUnit2 = ewOrder.Country.ToLower() == "ca" ? Math.Round(pricePerUnit2, 4) : 0,
                            szGlClass = " ",
                            szAdjustmentReasonCode = "CC",
                            cAdjustmentControlCode = "2",
                            cManualDiscount = "Y",
                            cPriceOverrideCode = "1",
                            cOrderLevelAdjustmentYN = "1",
                            cMutuallyExclusiveAdjustme = "0",
                            cPromotionDisplayControl = "0",
                            szUserId = ewOrder.UserId,
                            szProgramId = "RWWEB",
                            szWorkStationId = "P1",
                            jdDateUpdated = date,
                            mnTimeOfDay = Convert.ToInt32(time),
                            cNewBasePriceFlag = "0",
                            szDescriptionLine1 = "Discount",
                            szErrorDescription = " ",
                            mnTier = 99
                        });
                    }
                }
            }
        }
        // Call the add detail lines end-point
        json = JsonConvert.SerializeObject(orderDetail, settings);
        string result = SendWebApiMessage(apiUrl + "N554751", json).Result;
        WriteLog("Return from api call");
        detailReturn = new OrderDetailReturn
        {
            ServiceRequest1 = new ServiceRequest() { result = new Result() }
        };
        detailReturn.ServiceRequest1.submitted = true;
        WriteLog("Created return response");
        if (result != null)
        {
            try
            {
                WriteLog("Convert response");
                detailReturn = JsonConvert.DeserializeObject<OrderDetailReturn>(result);
                if (detailReturn.ServiceRequest1 == null)
                {
                    detailReturn.ServiceRequest1 = new ServiceRequest() { result = new Result() };
                    detailReturn.ServiceRequest1.submitted = true;
                    WriteLog("Changed submitted to true on error");
                    returnValue = "Error adding item detail to order";
                }
                else
                {
                    if (detailReturn.ServiceRequest1.submitted == false)
                    {
                        returnValue = "Error adding item detail to order";
                    }
                }
            }
            catch
            {
                // If the error is on the read response continue with no error
                detailReturn = new OrderDetailReturn
                {
                    ServiceRequest1 = new ServiceRequest() { result = new Result() }
                };
                detailReturn.ServiceRequest1.submitted = true;
                WriteLog("Changed submitted to true on error");
                returnValue = "Error adding item detail to order";
                ewOrder.Error = "Error adding item detail to order";
            }
        }
        // If details lines were added add the order header
        WriteLog("Submiited = " + detailReturn.ServiceRequest1.submitted.ToString());
        if (detailReturn.ServiceRequest1.submitted)
        {
            // If detail lines are added successfully call the price data end-point
            if (detailReturn.ServiceRequest1.submitted)
            {
                json = JsonConvert.SerializeObject(priceUpdate);
                _ = SendWebApiMessage(apiUrl + "N554074A", json);
            }

            string sHoldCode = " ";
            // string sOrderStatus = "";
            //string sZoneNumber = "";

            foreach (var door in ewOrder.DoorItems)
            {
                //    sOrderStatus = door.GLOrderStatus;
                //   sZoneNumber = door.GLReasonCodes;

                if (!string.IsNullOrWhiteSpace(door.HoldOrdersCode))
                {
                    sHoldCode = door.HoldOrdersCode;
                }
            }
            // Add order header to JDE
            OrderHeader orderHeader = new OrderHeader()
            {
                cEDIType = "1",
                szCompanyKeyEdiOrder = "00500",
                mnEdiDocumentNumber = ewOrder.DoorItems[0].OrderInfo.mnEdiDocumentNumber,
                szEdiDocumentType = "SZ",
                mnEdiLineNumber = "1",
                szEdiTransactionSet = "850",
                jdEdiTransmissionDate = date,
                cEdiSendRcvIndicator = "R",
                cEdiSuccessfullyProcess = "2",
                szEdiBatchNumber = ewOrder.DoorItems[0].OrderInfo.szEdiBatchNumber,
                szCompanyKeyOrderNo = "00500",
                mnDocumentOrderInvoiceE = ewOrder.DoorItems[0].OrderInfo.mnDocumentOrderInvoiceE,
                //  szOrderType = "SO",
                //szOrderType = ordertype,

                szOrderType = strOrderStatus,  //SO or N1 - ys
                szCostCenter = "50000",
                szCompany = "00500",
                mnAddressNumber = ewOrder.CustomerCode,
                //mnAddressNumber = "166",
                //mnAddressNumberShipTo = order.SHIP_TO_REF_NUM,
                mnAddressNumberShipTo = ewOrder.ShipTo,
                //mnAddressNumberShipTo = "1164",
                mnAddressNumberParent = ewOrder.CustomerCode,
                //mnAddressNumberParent = "166",
                jdDateRequestedJulian = !string.IsNullOrWhiteSpace(ewOrder.ShipDate) ? ewOrder.ShipDate : defaultshipdate,
                jdDateTransactionJulian = date,
                szReference1 = ewOrder.CustomerPo,
                szDeliveryInstructLine1 = ewOrder.JobTag,
                szDeliveryInstructLine2 = " ",
                //szRouteCode = ewOrder.ShipVia,
                szRouteCode = " ",
                // szFreightHandlingCode = "R",
                // mnCarrier = "120015",
                szFreightHandlingCode = " ",
                mnCarrier = "0",
                szModeOfTransport = "TL",
                //szZoneNumber = " ", //org
                szZoneNumber = strReasonCodes,  // 20 Jan 23, Reason Codes are assigned to szZoneNumber

                cCurrencyMode = "D",
                szOrderedBy = "WEBORDER",
                szOrderTakenBy = ewOrder.UserId,
                // szOrderTakenBy = "W_AD Name",
                szUserReservedReference = "",
                //szTransactionOriginator = order.USER_ID,
                szTransactionOriginator = "W_AD Name",
                szProgramId = "WEBORDER",
                szUserId = ewOrder.UserId,
                jdDateUpdated = date,
                mnTimeOfDay = time,
                nSourceOfOrder = " ",
                szIntegrationReference01 = ewOrder.SerialNum,
                szIntegrationReference02 = ewOrder.ConfigReference,
                szIntegrationReference03 = "0",
                // szIntegrationReference04 = " ",
                szIntegrationReference04 = ewOrder.SysproSalesOrder.StartsWith("5") ? ewOrder.SysproSalesOrder : " ",
                szIntegrationReference05 = ewOrder.SysproSalesOrder.StartsWith("5") ? "No PO" : " ",
                szHoldOrdersCode = sHoldCode,
                //szHoldOrdersCode = ewOrder.DoorItems[0].HoldOrdersCode == "E1" ? ewOrder.DoorItems[0].HoldOrdersCode : "E2",
                szUserReservedCode = ewOrder.SysproSalesOrder.StartsWith("5") ? "A" : " ",
                mnUserReservedAmount = ewOrder.Freight.ToString(),
                szPriceAdjustmentScheduleN = "RWEW",
                szErrorDescription = " "
            };
            // Call the order header end-point
            json = JsonConvert.SerializeObject(orderHeader, settings);
            result = SendWebApiMessage(apiUrl + "N554750", json).Result;
            try
            {
                OrderHeaderReturn headerReturn = JsonConvert.DeserializeObject<OrderHeaderReturn>(result);
                if (!string.IsNullOrWhiteSpace(headerReturn.sz55ErrorDescription))
                {
                    returnValue = headerReturn.sz55ErrorDescription;
                    ewOrder.Error = "Error adding header to order";
                }
            }
            catch
            {
                returnValue = "Error adding header to order";
                ewOrder.Error = "Error adding header to order";
            }
        }
        return returnValue;
    }

    // Process the order text
    private string AddOrderItemText(EWOrder ewOrder, string apiUrl)
    {
        string result = null;
        string doorsize;
        string inputtext;
        string[] mokey;
        string[] glazingPrefix = _configuration.GetValue<string>("AppSettings:GlazingPartPrefix").Split(',');
        bool found;
        int sbitemcnt = 1;
        OrderItem masterItem;
        foreach (var doorItem in ewOrder.DoorItems)
        {
            foreach (OrderItem item in doorItem.Items)
            {
                // Add media attachment for door item - checking for linetype or VAR_29 for sections or raw panel
                if (item.LineType == "Y" || item.VAR_29 == "ZZ" )
                {
                    WriteLog("Adding item text for " + ewOrder.SalesOrder + " - " + item.LineNum);
                    inputtext = "";
                   // mokey = new string[] { ewOrder.SalesOrder, "SO", "00500", Convert.ToDecimal(item.LineNum).ToString("0.000") };
                    mokey = new string[] { ewOrder.SalesOrder, strOrderStatus, "00500", Convert.ToDecimal(item.LineNum).ToString("0.000") };


                    try
                    {
                        if (!string.IsNullOrWhiteSpace(ewOrder.SysproSalesOrder) && ewOrder.SysproSalesOrder.StartsWith("5"))
                        {
                            inputtext += "<br><b>@@Old Sales Order# </b>" + ewOrder.SysproSalesOrder + "<br>";
                        }
                        doorsize = item.Door.WidthFt + "'" + item.Door.WidthIn + "\"X" + item.Door.HeightFt + "'" + item.Door.HeightIn + "\"(ACTUALDOORSIZE)";
                        inputtext += "<br><b>@@DOOR</b><br>&nbsp;&nbsp;&nbsp;&nbsp;Model: <b>" + doorItem.GLDoorModel + "</b>";
                        inputtext += "<br>&nbsp;&nbsp;&nbsp;&nbsp;Size: <b>" + doorItem.GLDoorSize + "</b>";
                        inputtext += "<br><b>@@SECTIONS</b><br>&nbsp;&nbsp;&nbsp;&nbsp;Number of Sections: <b>" + doorItem.GLNumberOfSection + "</b>";
                        inputtext += "<br>&nbsp;&nbsp;&nbsp;&nbsp;Exterior Colour: <b>" + doorItem.GLDoorColour + "</b>";
                        inputtext += "<br>&nbsp;&nbsp;&nbsp;&nbsp;Endcaps/End Stiles: <b>" + doorItem.GLEndCaps + "</b>";
                        inputtext += "<br>&nbsp;&nbsp;&nbsp;&nbsp;Style: <b>" + doorItem.GLStyle + "</b>";
                        inputtext += "<br>&nbsp;&nbsp;&nbsp;&nbsp;Top Weather Seal: <b>" + doorItem.GLTopWeatherSeal + "</b>";
                        inputtext += "<br>&nbsp;&nbsp;&nbsp;&nbsp;Bottom Seal: <b>" + doorItem.GLBottomSeal + "</b>";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GL_ALUM_BTM_SEC_TYPE) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Alumatite Bottom Section Type: <b>" + doorItem.GL_ALUM_BTM_SEC_TYPE + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLTrussStyle) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Trussing: <b>" + doorItem.GLTrussStyle + "</b>" : "";
                        inputtext += "<br><b>@@GLAZING</b><br>&nbsp;&nbsp;&nbsp;&nbsp;Glazing Type: <b>";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLGlazingType) ? doorItem.GLGlazingType + "</b>" : "None</b>";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GlALumGlazingType) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Alum Glazing Type: <b>" + doorItem.GlALumGlazingType + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLWindowType) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Window Type: <b>" + doorItem.GLWindowType + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLGlassType) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Glass Type: <b>" + doorItem.GLGlassType + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLFrameColour) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Frame Colour: <b>" + doorItem.GLFrameColour + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLLitesPerSpacing) ? "<br>>&nbsp;&nbsp;&nbsp;&nbsp;Lites Per Spacing: <b>" + doorItem.GLLitesPerSpacing + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLSpacing) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Spacing: <b>" + doorItem.GLSpacing + "</b>" : "";
                        inputtext += "<br><b>@@TRACK</b><br>&nbsp;&nbsp;&nbsp;&nbsp;Hardware Size: <b>";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLHardwareSize) ? doorItem.GLHardwareSize + "</b>" : "None</b>";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLLiftType) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Lift Type: <b>" + doorItem.GLLiftType + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLHlAmt) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;High Lift Amount: <b>" + doorItem.GLHlAmt + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLMountType) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Mount Type: <b>" + doorItem.GLMountType + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLJamb) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Jamb Type: <b>" + doorItem.GLJamb + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLShaftType) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Shaft Type: <b>" + doorItem.GLShaftType + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLSpringRH) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Spring RH: <b>" + doorItem.GLSpringRH + "</b>, Desc: <b>" + doorItem.GLSpringRhDesc + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLSpringLH) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Spring LH: <b>" + doorItem.GLSpringLH + "</b>, Desc: <b>" + doorItem.GLSpringLhDesc + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLExtensionSpring) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Extension Spring: <b>" + doorItem.GLExtensionSpring + "</b>" : "";
                        inputtext += "<br><b>@@Rolltite</b><br>&nbsp;&nbsp;&nbsp;&nbsp ";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLInvertedCurtain) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Inverted Cusrtain: <b>" + doorItem.GLInvertedCurtain + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLJambType) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Jamb Type: <b>" + doorItem.GLJambType + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLSlates) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Slats: <b>" + doorItem.GLSlates + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GlGuides) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Guides: <b>" + doorItem.GlGuides + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLElWl) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;ELWL: <b>" + doorItem.GLElWl + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLDrive) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Drive: <b>" + doorItem.GLDrive + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLCurtainRal) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Cusrtain Ral: <b>" + doorItem.GLCurtainRal + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLHoodRal) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Hood Ral: <b>" + doorItem.GLHoodRal + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLGuidesRal) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Guides Ral: <b>" + doorItem.GLGuidesRal + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLFasciaRal) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Fascia Ral: <b>" + doorItem.GLFasciaRal + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLBottomBarRal) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Bottom Bar Ral: <b>" + doorItem.GLBottomBarRal + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLJambGuideWS) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Jamb Guide Weather Seal: <b>" + doorItem.GLJambGuideWS + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLHeaderSeal) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Header Seal: <b>" + doorItem.GLHeaderSeal + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLLitesType) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Vision Lites: <b>" + doorItem.GLLitesType + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLLocks) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Locks: <b>" + doorItem.GLLocks + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLSlopedBottomBar) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Sloped Bottom bare: <b>" + doorItem.GLSlopedBottomBar + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLHood) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Hood: <b>" + doorItem.GLHood + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLMasonryClip) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Masonry Clip: <b>" + doorItem.GLMasonryClip + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLMountingPlates) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Mounting Plates: <b>" + doorItem.GLMountingPlates + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GlSupportBrackets) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Support Brackets: <b>" + doorItem.GlSupportBrackets + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLPerforatedSlats) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Perforated Slats: <b>" + doorItem.GLPerforatedSlats + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GlBottomBar) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Bottom Bar: <b>" + doorItem.GlBottomBar + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLHourRating) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Bottom Bar: <b>" + doorItem.GLHourRating + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLSpringCycleLife) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Bottom Bar: <b>" + doorItem.GLSpringCycleLife + "</b>" : "";
                        inputtext += !string.IsNullOrWhiteSpace(doorItem.GLFusibleLink) ? "<br>&nbsp;&nbsp;&nbsp;&nbsp;Bottom Bar: <b>" + doorItem.GLFusibleLink + "</b>" : "";


                        //inputtext += "<br>" + item.Description;
                        if (!string.IsNullOrWhiteSpace(item.Door.WidthFt))
                        {
                            TextAttachment textAttachment = new TextAttachment()
                            {
                                moStructure = "GT4211A",
                                moKey = mokey,
                                formName = "",
                                version = "",
                                itemName = "Ack Text",
                                inputText = inputtext
                            };
                            string json = JsonConvert.SerializeObject(textAttachment);
                            result = SendWebApiMessage(apiUrl + "addtext", json).Result;
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteLog("Error in adding item text " + ex.Message);
                        result = "";
                    }
                    inputtext = "";
                    mokey = null;
                }
                // Add media attachment for glazing item
                found = false;
                foreach (var prefix in glazingPrefix)
                {
                    if (item.ItemNum.StartsWith(prefix) && (item.Description.StartsWith("SB-BG") || item.Description.StartsWith("SB-IG") || item.Description.StartsWith("SB-TG")))
                    {
                        found = true;
                    }
                    if (item.ItemNum.StartsWith(prefix) && (item.Description.Contains("A150") || item.Description.Contains("A175") || item.Description.Contains("A200") || item.Description.Contains("A300") || item.Description.Contains("A300C")))
                    {
                        found = true;
                    }
                }
                if (found && doorItem.GlazingNotes != null)
                {
                    WriteLog("Adding glazing text for " + ewOrder.SalesOrder + " - " + item.LineNum);
                   // mokey = new string[] { ewOrder.SalesOrder, "SO", "00500", Convert.ToDecimal(item.LineNum).ToString("0.000") };
                    mokey = new string[] { ewOrder.SalesOrder, strOrderStatus, "00500", Convert.ToDecimal(item.LineNum).ToString("0.000") };


                    inputtext = "Glazed with " + item.Description;
                    if (!string.IsNullOrWhiteSpace(doorItem.GlazingNotes))
                    {
                        inputtext += "<br><br>" + doorItem.GlazingNotes;
                        // Get part descriptions for parts listed in the glazing notes
                        string[] parts = doorItem.GlazingNotes.Split(',');
                        OrderItem orderItem;
                        foreach (var part in parts)
                        {
                            orderItem = doorItem.Items.Where(x => x.ItemNum == part.Trim()).FirstOrDefault();
                            if (orderItem != null)
                            {
                                inputtext += "<br><br>" + orderItem.ItemNum + " - " + orderItem.Description;
                            }
                        }
                    }
                    TextAttachment textAttachment = new TextAttachment()
                    {
                        moStructure = "GT4211A",
                        moKey = mokey,
                        formName = "",
                        version = "",
                        itemName = "Glazing Text",
                        inputText = inputtext
                    };
                    string json = JsonConvert.SerializeObject(textAttachment);
                    result = SendWebApiMessage(apiUrl + "addtext", json).Result;
                    inputtext = "";
                    mokey = null;
                }
                // Add media attachment for cut to instructions
                if (item.CutInstructions != null)
                {
                    if (item.CutInstructions.Count > 0)
                    {
                        WriteLog("Adding cut instructions text for " + ewOrder.SalesOrder + " - " + item.LineNum);
                        inputtext = "";
                        foreach (var cutInstruction in item.CutInstructions)
                        {
                            inputtext += cutInstruction + "<br/><br/>";
                        }
                    //    mokey = new string[] { ewOrder.SalesOrder, "SO", "00500", Convert.ToDecimal(item.LineNum).ToString("0.000") };
                        mokey = new string[] { ewOrder.SalesOrder, strOrderStatus, "00500", Convert.ToDecimal(item.LineNum).ToString("0.000") };

                        TextAttachment textAttachment = new TextAttachment()
                        {
                            moStructure = "GT4211A",
                            moKey = mokey,
                            formName = "",
                            version = "",
                            itemName = "Cut Instruction",
                            inputText = inputtext
                        };
                        string json = JsonConvert.SerializeObject(textAttachment);
                        result = SendWebApiMessage(apiUrl + "addtext", json).Result;
                    }
                }
                // Add media attachments for SB sections
                inputtext = "";
                if (item.Description.StartsWith("SB-B ") && !string.IsNullOrWhiteSpace(doorItem.MA_SB_BTM))
                {
                    inputtext = doorItem.MA_SB_BTM;
                }
                if (item.Description.StartsWith("SB-G ") && !string.IsNullOrWhiteSpace(doorItem.MA_SB_GLZ))
                {
                    inputtext = doorItem.MA_SB_GLZ;
                }
                if (item.Description.StartsWith("SB-I "))
                {
                    if (sbitemcnt == 1)
                    {
                        if (!string.IsNullOrWhiteSpace(doorItem.MA_SB_INT_1))
                        {
                            inputtext = doorItem.MA_SB_INT_1;
                            sbitemcnt++;
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(doorItem.MA_SB_INT_2))
                        {
                            inputtext = doorItem.MA_SB_INT_2;
                        }
                    }
                }
                if ((item.Description.StartsWith("GF") || item.Description.StartsWith("DF")) && !string.IsNullOrWhiteSpace(doorItem.GLMediaAttachmentSB))
                {
                    inputtext = doorItem.GLMediaAttachmentSB;
                }
                if (inputtext != "")
                {
                    mokey = new string[] { ewOrder.SalesOrder, strOrderStatus, "00500", Convert.ToDecimal(item.LineNum).ToString("0.000") };

                    TextAttachment textAttachment = new TextAttachment()
                    {
                        moStructure = "GT4211A",
                        moKey = mokey,
                        formName = "",
                        version = "",
                        itemName = "Milestone",
                        inputText = inputtext
                    };
                    string json = JsonConvert.SerializeObject(textAttachment);
                    result = SendWebApiMessage(apiUrl + "addtext", json).Result;
                }
            }
        }
        return result;
    }

    // Get access token
    private AccessToken GetAccessToken(string apiUrl, string deviceID, string userName, string userPassword)
    {
        Stream stream = null;
        HttpWebRequest webRequest;
        HttpWebResponse webResponse = null;
        AccessToken token = null;
        try
        {
            //Call JDE To create new Token
            webRequest = (HttpWebRequest)WebRequest.Create(apiUrl);
            //webRequest.Timeout = 600000;
            webRequest.Timeout = 600000;
            webRequest.Method = "POST";
            webRequest.Accept = "application/json";
            webRequest.ContentType = "application/json";
            //webRequest.Connection = "keep-alive";

            //// Parameters to Object<String, String> Array. 
            var form = new Dictionary<string, string>
                    {
                        { "deviceName", deviceID },
                        { "username",  userName },
                        { "password", userPassword }
                    };

            //Convert Parameters to JSON for Post
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(form);

            //Writes the json to web body content.
            using (var streamWriter = new StreamWriter(webRequest.GetRequestStream()))
            {
                //string json = "{ \"method\" : \"guru.test\", \"params\" : [ \"Guru\" ], \"id\" : 123 }";
                streamWriter.Write(json);
                streamWriter.Flush();
            }

            //Get Response
            webResponse = (HttpWebResponse)webRequest.GetResponse();

            if (webResponse.StatusCode == HttpStatusCode.OK)
            {
                stream = webResponse.GetResponseStream();
                if (stream.CanRead)
                {
                    //Read Result Content into a variable to convert to defined C# class
                    StreamReader sr = new StreamReader(stream);
                    var result = sr.ReadToEnd();
                    token = JsonConvert.DeserializeObject<AccessToken>(result);
                }

                stream.Close();
            }
            else
            {
                throw new Exception(webResponse.StatusDescription);
            }
        }
        catch (Exception ex)
        {
            string s = ex.Message;
            token = null;
        }
        finally
        {
            if (stream != null)
            {
                stream.Close();
                stream.Dispose();
            }

            if (webResponse != null)
            {
                webResponse.Close();
                webResponse.Dispose();
            }
        }
        return token;
    }

    // Call the JDE Web Api
    private async Task<string> SendWebApiMessage(string url, string json)
    {
        WriteLog(json);
        WriteLog(url);

        string result = "";

        // This is the async methods that are not returning from some calls
        HttpContent httpContent = null;
        HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        await httpClient.SendAsync(httpRequest).ContinueWith(responseTask =>
        {
            try
            {
                httpContent = responseTask.Result.Content;
                result = httpContent.ReadAsStringAsync().Result;
            }
            catch (Exception ex) { WriteLog(ex.Message); }
        });

        WriteLog(result);
        return result;
    }

    // Send a mail message
    static void SendMail(string subject, string body, string email)
    {
        MimeMessage message = new MimeMessage();
        try
        {
            message.From.Add(MailboxAddress.Parse("dnr@rwdoors.com"));
            List<string> emails = email.Split(',').ToList();
            foreach (var item in emails)
            {
                message.To.Add(MailboxAddress.Parse(item));
            }
            message.Subject = subject;
            message.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = body };
        }
        catch { message = null; }

        if (message != null)
        {
            using var client = new SmtpClient();
            client.Connect("smtp.office365.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
            client.Authenticate("dnr@rwdoors.com", "password4ewservice%");
            try { client.Send(message); }
            catch (Exception ex) { string s = ex.Message; }
            client.Disconnect(true);
            client.Dispose();
        }
        
        if (message != null)
        {
            //SmtpClient client = new SmtpClient("smtp.office365.com", 587);
            //client.Credentials = new NetworkCredential("dnr@rwdoors.com", "password4ewservice%");
            //client.EnableSsl = true;
            //client.UseDefaultCredentials = false;
            //client.DeliveryMethod = SmtpDeliveryMethod.Network;
            //try { client.Send(message); }
            //catch (Exception ex) { string s = ex.Message; }
            //client.Dispose();
            //message.Dispose();
        }
    }
    static void SendSPRMail(string subject, string body, string email)
    {
        MimeMessage message = new MimeMessage();
        try
        {
            message.From.Add(MailboxAddress.Parse("dnr@rwdoors.com"));
            List<string> emails = email.Split(',').ToList();
            foreach (var item in emails)
            {
                message.To.Add(MailboxAddress.Parse(item));
            }
            message.Subject = subject;
            message.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = body };
        }
        catch { message = null; }

        if (message != null)
        {
            using var client = new SmtpClient();
            client.Connect("smtp.office365.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
            client.Authenticate("dnr@rwdoors.com", "password4ewservice%");
            try { client.Send(message); }
            catch (Exception ex) { string s = ex.Message; }
            client.Disconnect(true);
            client.Dispose();
        }

        if (message != null)
        {
            //SmtpClient client = new SmtpClient("smtp.office365.com", 587);
            //client.Credentials = new NetworkCredential("dnr@rwdoors.com", "password4ewservice%");
            //client.EnableSsl = true;
            //client.UseDefaultCredentials = false;
            //client.DeliveryMethod = SmtpDeliveryMethod.Network;
            //try { client.Send(message); }
            //catch (Exception ex) { string s = ex.Message; }
            //client.Dispose();
            //message.Dispose();
        }
    }

    // Write to the application log file
    private void WriteLog(string text)
    {
        StreamWriter sw = new StreamWriter(logfile, true);
        if (sw != null)
        {
            sw.WriteLine("\r\n" + DateTime.Now + " - " + text);
            sw.Flush();
            sw.Close();
            sw.Dispose();
        }
    }

    public class JdeDate
    {
        /// <summary>
        /// This will convert a julian date to a gregorian date.
        /// </summary>
        /// <param name="jDate"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        public DateTime JulianToGregorian(int jDate, int time)
        {
            string RealJulian = (jDate + 1900000).ToString();
            string strTime = time.ToString("000000");

            DateTime dt = new DateTime(int.Parse(RealJulian[..4]), 1, 1, int.Parse(strTime[..2]), int.Parse(strTime.Substring(2, 2)), int.Parse(strTime.Substring(4, 2)));
            if (jDate > 0)
                return dt.AddDays(int.Parse(RealJulian[4..]) - 1);
            else
                return dt;
        }

        /// <summary>
        /// This will convert a gregorian date to a julian date.
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public int GregorianToJulian(DateTime date)
        {
            return (date.Year * 1000) - 1900000 + date.DayOfYear;
        }

        /// <summary>
        /// This will convert time of day to an int.
        /// </summary>
        /// <param name="ticks"></param>
        /// <returns></returns>
        public int TimeOfDayToInt(long ticks)
        {
            TimeSpan _ts = new TimeSpan(ticks);
            return (_ts.Hours * 10000) + (_ts.Minutes * 100) + _ts.Seconds;
        }
    }
}
