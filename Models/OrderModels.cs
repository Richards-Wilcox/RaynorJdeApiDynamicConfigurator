using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RaynorJdeApi.Models
{
    public class EWOrder
    {
        public string CustomerPo { get; set; }
        public string ShipDate { get; set; }
        public string OrderType { get; set; }
        public string SPR { get; set; }
        public string SPR_DETAIL { get; set; }
        public string JobTag { get; set; }
        public string ShipVia { get; set; }
        public string Country { get; set; }
        public string ShipTo { get; set; }
        public string UserId { get; set; }
        public double Freight { get; set; }
        public List<DoorItem> DoorItems { get; set; }
        public string DocumentNum { get; set; }
        public string SalesOrder { get; set; }
        public string LotNumber { get; set; }
        public string SysproSalesOrder { get; set; }
        public string ConfigReference { get; set; }
        public string QuoteReference { get; set; }
        public string CustomerCode { get; set; }
        public string SerialNum { get; set; }
        public bool Submitted { get; set; }
        public bool CreatePO { get; set; }
        public List<string> Var25 { get; set; }
        public List<OrderItem> Var25Item { get; set; }
        public List<string> ExcludedItem { get; set; }
        public string Error { get; set; }
        public string YLineItem { get; set; }
    }

    public class DoorItem
    {
        public OrderInfo OrderInfo { get; set; }
        public List<OrderItem> Items { get; set; }
        public string OrderTag { get; set; }
        public int WindowQuantity { get; set; }
        public string GlazingNotes { get; set; }
        public string GlazingRunTime { get; set; }
        public bool Exclude { get; set; }
        public string HoldOrdersCode { get; set; }
        public string GLDoorModel { get; set; }
        public string GLDoorSize { get; set; }
        public string GLNumberOfSection { get; set; }
        public string GLDoorColour { get; set; }
        public string GLStyle { get; set; }
        public string GLEndCaps { get; set; }
        public string GLLiftType { get; set; }
        public string GLTopWeatherSeal { get; set; }
        public string GLBottomSeal { get; set; }
        public string GLTrussStyle { get; set; }
        public string GLGlazingType { get; set; }
        public string GlALumGlazingType { get; set; }
        public string GLWindowType { get; set; }
        public string GLGlassType { get; set; }
        public string GLFrameColour { get; set; }
        public string GLLitesPerSpacing { get; set; }
        public string GLSpacing { get; set; }
        public string GLHardwareSize { get; set; }
        public string GLMountType { get; set; }
        public string GLJamb { get; set; }
        public string GLShaftType { get; set; }
        public string GLSpringRH { get; set; }
        public string GLSpringLH { get; set; }
        public string GLSpringRhDesc { get; set; }
        public string GLSpringLhDesc { get; set; }
        public string GLExtensionSpring { get; set; }
        public string GLHlAmt { get; set; }
        public string MA_SB_BTM { get; set; }
        public string MA_SB_GLZ { get; set; }
        public string MA_SB_INT_1 { get; set; }
        public string MA_SB_INT_2 { get; set; }
        public string GLMediaAttachmentSB { get; set; }
        public string GLZ_CODE_SB_RP_1 { get;  set; }
        public string GLZ_CODE_SB_RP_2 { get; set; }
        public string GLZ_CODE_INT_1_RP_1 { get; set; }
        public string GLZ_CODE_INT_1_RP_2 { get; set; }
        public string GLZ_CODE_INT_2_RP_1 { get; set; }
        public string GLZ_CODE_INT_2_RP_2 { get; set; }
        public string GLZ_CODE_INT_3_RP_1 { get; set; }
        public string GLZ_CODE_INT_3_RP_2 { get; set; }
        public string CNC_SB_RP1 { get; set; }
        public string CNC_SB_RP2 { get; set; }
        public string CNC_INT1_RP1 { get; set; }
        public string CNC_INT1_RP2 { get; set; }
        public string CNC_INT2_RP1 { get; set; }
        public string CNC_INT2_RP2 { get; set; }
        public string CNC_INT3_RP1 { get; set; }
        public string CNC_INT3_RP2 { get; set; }
        public string PANEL_CODE { get; set; }
        public string PC_SB_RP1 { get; set; }
        public string PC_SB_RP2 { get; set; }
        public string PC_INT1_RP1 { get; set; }
        public string PC_INT1_RP2 { get; set; }
        public string PC_INT2_RP1 { get; set; }
        public string PC_INT2_RP2 { get; set; }
        public string PC_INT3_RP1 { get; set; }
        public string PC_INT3_RP2 { get; set; }
        public string SB_RP_1_PANEL_TYPE { get; set; }
        public string SB_RP_2_PANEL_TYPE { get; set; }
        public string INT1_RP1_PANEL_TYPE { get; set; }
        public string INT1_RP2_PANEL_TYPE { get; set; }
        public string INT2_RP1_PANEL_TYPE { get; set; }
        public string INT2_RP2_PANEL_TYPE { get; set; }
        public string INT3_RP1_PANEL_TYPE { get; set; }
        public string INT3_RP2_PANEL_TYPE { get; set; }
        public string SB_RP_1_ORPHAN { get; set; }
        public string SB_RP_2_ORPHAN { get; set; }
        public string INT1_RP_1_ORPHAN { get; set; }
        public string INT1_RP_2_ORPHAN { get; set; }
        public string INT2_RP_1_ORPHAN { get; set; }
        public string INT2_RP_2_ORPHAN { get; set; }
        public string INT3_RP_1_ORPHAN { get; set; }
        public string INT3_RP_2_ORPHAN { get; set; }
        public string SB_RP1_PANEL_CONFIGURATION { get; set; }
        public string SB_RP2_PANEL_CONFIGURATION { get; set; }
        public string INT1_RP1_PANEL_CONFIGURATION { get; set; }
        public string INT1_RP2_PANEL_CONFIGURATION { get; set; }
        public string INT2_RP1_PANEL_CONFIGURATION { get; set; }
        public string INT2_RP2_PANEL_CONFIGURATION { get; set; }
        public string INT3_RP1_PANEL_CONFIGURATION { get; set; }
        public string INT3_RP2_PANEL_CONFIGURATION { get; set; }
        public string GLInvertedCurtain { get; set; }
        public string GLJambType { get; set; }
        public string GLSlates { get; set; }
        public string GlGuides { get; set; }
        public string GLElWl { get; set; }
        public string GLDrive { get; set; }
        public string GLCurtainRal { get; set; }
        public string GLHoodRal { get; set; }
        public string GLGuidesRal { get; set; }
        public string GLFasciaRal { get; set; }
        public string GLBottomBarRal { get; set; }
        public string GLJambGuideWS { get; set; }
        public string GLHeaderSeal { get; set; }
        public string GLLitesType { get; set; }
        public string GLLocks { get; set; }
        public string GLSlopedBottomBar { get; set; }
        public string GLHood { get; set; }
        public string GLMasonryClip { get; set; }
        public string GLMountingPlates { get; set; }
        public string GlSupportBrackets { get; set; }
        public string GLPerforatedSlats { get; set; }
        public string GlBottomBar { get; set; }
        public string GLHourRating { get; set; }
        public string GLSpringCycleLife { get; set; }
        public string GLFusibleLink { get; set; }
        public string SEC_1_SEC_BDL_RP { get; set; }
        public string SEC_2_SEC_BDL_RP { get; set; }
        public string SEC_3_SEC_BDL_RP { get; set; }
        public string SEC_4_SEC_BDL_RP { get; set; }
        public string SEC_5_SEC_BDL_RP { get; set; }
        public string SEC_6_SEC_BDL_RP { get; set; }
        public string SEC_7_SEC_BDL_RP { get; set; }
        public string SEC_8_SEC_BDL_RP { get; set; }
        public string SEC_9_SEC_BDL_RP { get; set; }
        public string SEC_10_SEC_BDL_RP { get; set; }

        public string SEC_BTM_SEC_BDL_RP { get; set; }
        public string SEC_1 { get; set; }
        public string SEC_2 { get; set; }
        public string SEC_3 { get; set; }
        public string SEC_4 { get; set; }
        public string SEC_5 { get; set; }
        public string SEC_6 { get; set; }
        public string SEC_7 { get; set; }
        public string SEC_8 { get; set; }
        public string SEC_9 { get; set; }
        public string SEC_10 { get; set; }
        public string SEC_BTM { get; set; }
        public string SEC_1_PANEL_QTY { get; set; }
        public string SEC_2_PANEL_QTY { get; set; }
        public string SEC_3_PANEL_QTY { get; set; }
        public string SEC_4_PANEL_QTY { get; set; }
        public string SEC_5_PANEL_QTY { get; set; }
        public string SEC_6_PANEL_QTY { get; set; }
        public string SEC_7_PANEL_QTY { get; set; }
        public string SEC_8_PANEL_QTY { get; set; }
        public string SEC_9_PANEL_QTY { get; set; }
        public string SEC_10_PANEL_QTY { get; set; }
        public string SEC_BTM_PANEL_QTY { get; set; }
        public string PANEL_CONFIGURATION_SEC_1 { get; set; }
        public string PANEL_CONFIGURATION_SEC_2 { get; set; }
        public string PANEL_CONFIGURATION_SEC_3 { get; set; }
        public string PANEL_CONFIGURATION_SEC_4 { get; set; }
        public string PANEL_CONFIGURATION_SEC_5 { get; set; }
        public string PANEL_CONFIGURATION_SEC_6 { get; set; }
        public string PANEL_CONFIGURATION_SEC_7 { get; set; }
        public string PANEL_CONFIGURATION_SEC_8 { get; set; }
        public string PANEL_CONFIGURATION_SEC_9 { get; set; }
        public string PANEL_CONFIGURATION_SEC_10 { get; set; }
        public string PANEL_CONFIGURATION_SEC_BTM { get; set; }
        public string GL_ALUM_BTM_SEC_TYPE { get; set; }
        public string SB_RP_1_DOOR_MODEL { get; set; }
        public string SB_RP_2_DOOR_MODEL { get; set; }
        public string INT1_RP_1_DOOR_MODEL { get; set; }
        public string INT1_RP_2_DOOR_MODEL { get; set; }
        public string INT2_RP_1_DOOR_MODEL { get; set; }
        public string INT2_RP_2_DOOR_MODEL { get; set; }
        public string INT3_RP_1_DOOR_MODEL { get; set; }
        public string INT3_RP_2_DOOR_MODEL { get; set; }
        public string SB_RP_1_PANEL_STYLE { get; set; }
        public string SB_RP_2_PANEL_STYLE { get; set; }
        public string INT1_RP_1_PANEL_STYLE { get; set; }
        public string INT1_RP_2_PANEL_STYLE { get; set; }
        public string INT2_RP_1_PANEL_STYLE { get; set; }
        public string INT2_RP_2_PANEL_STYLE { get; set; }
        public string INT3_RP_1_PANEL_STYLE { get; set; }
        public string INT3_RP_2_PANEL_STYLE { get; set; }
        public string SB_RP_1_DOOR_COLOUR { get; set; }
        public string SB_RP_2_DOOR_COLOUR { get; set; }
        public string INT1_RP_1_DOOR_COLOUR { get; set; }
        public string INT1_RP_2_DOOR_COLOUR { get; set; }
        public string INT2_RP_1_DOOR_COLOUR { get; set; }
        public string INT2_RP_2_DOOR_COLOUR { get; set; }
        public string INT3_RP_1_DOOR_COLOUR { get; set; }
        public string INT3_RP_2_DOOR_COLOUR { get; set; }
        public string SB_RP_1_DRILL_FOR_HINGES { get; set; }
        public string SB_RP_2_DRILL_FOR_HINGES { get; set; }
        public string INT1_RP_1_DRILL_FOR_HINGES { get; set; }
        public string INT1_RP_2_DRILL_FOR_HINGES { get; set; }
        public string INT2_RP_1_DRILL_FOR_HINGES { get; set; }
        public string INT2_RP_2_DRILL_FOR_HINGES { get; set; }
        public string INT3_RP_1_DRILL_FOR_HINGES { get; set; }
        public string INT3_RP_2_DRILL_FOR_HINGES { get; set; }
        public string SB_RP_1_DRILL_CODE { get; set; }
        public string SB_RP_2_DRILL_CODE { get; set; }
        public string INT1_RP_1_DRILL_CODE { get; set; }
        public string INT1_RP_2_DRILL_CODE { get; set; }
        public string INT2_RP_1_DRILL_CODE { get; set; }
        public string INT2_RP_2_DRILL_CODE { get; set; }
        public string INT3_RP_1_DRILL_CODE { get; set; }
        public string INT3_RP_2_DRILL_CODE { get; set; }
        public string SB_RP_1_GLAZED { get; set; }
        public string SB_RP_2_GLAZED { get; set; }
        public string INT1_RP_1_GLAZED { get; set; }
        public string INT1_RP_2_GLAZED { get; set; }
        public string INT2_RP_1_GLAZED { get; set; }
        public string INT2_RP_2_GLAZED { get; set; }
        public string INT3_RP_1_GLAZED { get; set; }
        public string INT3_RP_2_GLAZED { get; set; }
        public string SB_RP_1_BOTTOM_RTNR_SEAL { get; set; }
        public string SB_RP_2_BOTTOM_RTNR_SEAL { get; set; }
        public string INT1_RP_1_BOTTOM_RTNR_SEAL { get; set; }
        public string INT1_RP_2_BOTTOM_RTNR_SEAL { get; set; }
        public string INT2_RP_1_BOTTOM_RTNR_SEAL { get; set; }
        public string INT2_RP_2_BOTTOM_RTNR_SEAL { get; set; }
        public string INT3_RP_1_BOTTOM_RTNR_SEAL { get; set; }
        public string INT3_RP_2_BOTTOM_RTNR_SEAL { get; set; }
        public string SB_RP_1_END_CAP { get; set; }
        public string SB_RP_2_END_CAP { get; set; }
        public string INT1_RP_1_END_CAP { get; set; }
        public string INT1_RP_2_END_CAP { get; set; }
        public string INT2_RP_1_END_CAP { get; set; }
        public string INT2_RP_2_END_CAP { get; set; }
        public string INT3_RP_1_END_CAP { get; set; }
        public string INT3_RP_2_END_CAP { get; set; }
        public string SB_RP_1_PANEL_SEQUENCE { get; set; }
        public string SB_RP_2_PANEL_SEQUENCE { get; set; }
        public string INT1_RP_1_PANEL_SEQUENCE { get; set; }
        public string INT1_RP_2_PANEL_SEQUENCE { get; set; }
        public string INT2_RP_1_PANEL_SEQUENCE { get; set; }
        public string INT2_RP_2_PANEL_SEQUENCE { get; set; }
        public string INT3_RP_1_PANEL_SEQUENCE { get; set; }
        public string INT3_RP_2_PANEL_SEQUENCE { get; set; }
        public string SB_RP_1_SMART_COM_CODE { get; set; }
        public string SB_RP_2_SMART_COM_CODE { get; set; }
        public string INT1_RP_1_SMART_COM_CODE { get; set; }
        public string INT1_RP_2_SMART_COM_CODE { get; set; }
        public string INT2_RP_1_SMART_COM_CODE { get; set; }
        public string INT2_RP_2_SMART_COM_CODE { get; set; }
        public string INT3_RP_1_SMART_COM_CODE { get; set; }
        public string INT3_RP_2_SMART_COM_CODE { get; set; }
        public string SB_RP_1_DF_SEQ { get; set; }
        public string SB_RP_2_DF_SEQ { get; set; }
        public string INT1_RP_1_DF_SEQ { get; set; }
        public string INT1_RP_2_DF_SEQ { get; set; }
        public string INT2_RP_1_DF_SEQ { get; set; }
        public string INT2_RP_2_DF_SEQ { get; set; }
        public string INT3_RP_1_DF_SEQ { get; set; }
        public string INT3_RP_2_DF_SEQ { get; set; }
        public string SB_RP_1_WIDTH_CODE { get; set; }
        public string SB_RP_2_WIDTH_CODE { get; set; }
        public string INT1_RP_1_WIDTH_CODE { get; set; }
        public string INT1_RP_2_WIDTH_CODE { get; set; }
        public string INT2_RP_1_WIDTH_CODE { get; set; }
        public string INT2_RP_2_WIDTH_CODE { get; set; }
        public string INT3_RP_1_WIDTH_CODE { get; set; }
        public string INT3_RP_2_WIDTH_CODE { get; set; }

    }

    public class OrderInfo
    {
        public string mnEdiDocumentNumber { get; set; }
        public string szEdiBatchNumber { get; set; }
        public string mnDocumentOrderInvoiceE { get; set; }
        public string szErrorDescription { get; set; }
    }

    public class OrderItem
    {
        public string ItemNum { get; set; }
        public int ShortItemNum { get; set; }
        public string UOM { get; set; }
        public string Description { get; set; }
        public List<BOM> BOMs { get; set; }
        public DoorInfo Door { get; set; }
        public float Quantity { get; set; }
        public double UnitPrice { get; set; }
        public double Discount { get; set; }
        public int LineNum { get; set; }
        public int ParentLineNum { get; set; }
        public string LineType { get; set; }
        public string EasyWebLineType { get; set; }
        public string StockingType { get; set; }
        public int RefLineNum { get; set; }
        public string LotNumber { get; set; }
        public string Weight { get; set; }
        public string Taxable { get; set; }
        public string LineType2 { get; set; }
        public string BuyerNumber { get; set; }
        public string LeadTime { get; set; }
        public string CommodityCode { get; set; }
        public string SalesCat1 { get; set; }
        public string SalesCat2 { get; set; }
        public string SalesCat3 { get; set; }
        public string SalesCat4 { get; set; }
        public string MasterPlanFamily { get; set; }
        public string StockRunCode { get; set; }
        public string VendorNum { get; set; }
        public bool Add { get; set; }
        public bool AddItemDetail { get; set; }
        public string SmartPartNum { get; set; }
        public string Supplier { get; set; }
        public string ItemCost { get; set; }
        public string ComRunTime { get; set; }
        public List<RoutingText> RoutingTextList { get; set; }
        public string CutInstPart { get; set; }
        public List<string> CutInstructions { get; set; }
        public string VAR_29 { get; set; }
    }

    public class DoorInfo
    {
        public string Model { get; set; }
        public string HeightFt { get; set; }
        public string HeightIn { get; set; }
        public string WidthFt { get; set; }
        public string WidthIn { get; set; }
        public string Colour { get; set; }
    }

    public class BOM
    {
        public string ItemNum { get; set; }
        public int ShortItemNum { get; set; }
        public string Quantity { get; set; }
        public string Branch { get; set; }
        public string Description { get; set; }
        public bool Add { get; set; }
        public List<BOM> BOMs { get; set; }
        public string CutInstPart { get; set; }
        public List<string> CutInstructions { get; set; }
    }

    public class RoutingText
    {
        public string ItemNum { get; set; }
        public string ShortItemNum { get; set; }
        public List<RoutingOperation> Operations { get; set; }
    }

    public class RoutingOperation
    {
        public string SequenceNumber { get; set; }
        public string WorkCenter { get; set; }
        public string RunLabor { get; set; }
        public string Description { get; set; }
    }
}
