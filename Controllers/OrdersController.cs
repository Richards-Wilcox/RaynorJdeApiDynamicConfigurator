using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace RaynorJdeApiDynamicConfigurator.Controllers
{
    [SupportedOSPlatform("Windows")]
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController(IConfiguration configuration) : ControllerBase
    {
        private readonly ConceptService conceptService = new(configuration);
        private readonly string ordersPath = configuration.GetValue<string>("AppSettings:OrdersTextFilePath");

        // GET: api/<OrdersController>
        [HttpGet("{file}")]
        public async void Get(string file)
        {
            string logfile = "c:\\jdelog\\raynorjdeapilog_Orders" + "_" + DateTime.Now.ToString("MMddyyyymmssfff") + ".txt";
            StreamReader sr = null;
            if (!string.IsNullOrWhiteSpace(ordersPath))
            {
                file = ordersPath + "\\" + file;
            }
            WriteLog(logfile, "Processing file " + file);
            try { sr = new StreamReader(file + ".txt"); }
            catch { }
            if (sr != null)
            {
                string order;
                while (!sr.EndOfStream)
                {
                    order = sr.ReadLine();
                    WriteLog(logfile, "Processing order " + order);
                    await conceptService.fireOrder(order);
                }
                sr.Close();
                sr.Dispose();
            }
        }

        // Write to the application log file
        private static void WriteLog(string logfile, string text)
        {
            StreamWriter sw = new(logfile, true);
            if (sw != null)
            {
                sw.WriteLine("\r\n" + DateTime.Now + " - " + text);
                sw.Flush();
                sw.Close();
                sw.Dispose();
            }
        }
    }
}
