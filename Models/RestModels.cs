using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RaynorJdeApi.Models
{
    public class ItemQuery
    {
        public List<Item> ItemArray { get; set; }
        public string webOrderNbr { get; set; }
        public int configurationNbr { get; set; }
    }

    public class ItemQueryReturn
    {
        public List<Item> N554130_Repeating { get; set; }
    }

    public class Item
    {
        public string szIdentifierShortItem { get; set; }
        public string szIdentifier2ndItem { get; set; }
        public string sz55ErrorDescription { get; set; }
    }

    public class ItemUpdate
    {
        public List<ItemAdd> NewItemArray { get; set; }
    }

    public class ItemAdd
    {
        public string Product_No { get; set; }
        public string Search_Text { get; set; }
        public string Stocking_Type { get; set; }
        public string G_L_Class { get; set; }
        public string Unit_of_Measure { get; set; }
        public string Planner_Number_ALKY { get; set; }
        public string Buyer_Number_ALKY { get; set; }
        public string Sales_Price_Level { get; set; }
        public string Item_Price_Group { get; set; }
        public string Sales_Catalog_Section { get; set; }
        public string Sub_Section { get; set; }
        public string Sales_Category_Code_3 { get; set; }
        public string Sales_Category_Code_4 { get; set; }
        public string Commodity_Class { get; set; }
        public string Master_Planning_Family { get; set; }
        public string Warehouse_Process_Grp_1 { get; set; }
        public string Category_Code_6 { get; set; }
        public string Category_Code_7 { get; set; }
        public string Category_Code_8 { get; set; }
        public string Category_Code_9 { get; set; }
        public string Planning_Code { get; set; }
        public string Issue_Type_Code { get; set; }
        public string Branch_Plant { get; set; }
        public string Supplier_Number_ALKY { get; set; }
        public string P4101_Version { get; set; }
        public string Description1 { get; set; }
        public string Description2 { get; set; }
        public int Leadtime_Level { get; set; }
        public string Item_Pool_Code_PRP0 { get; set; }
        public string Line_Type_LNTY { get; set; }
        public string Inventory_Cost_Level_CLEV { get; set; }
        public string Purchase_Price_Level_PPLV { get; set; }
        public string Supplier_Rebate_Code_PRP3 { get; set; }
        public string Category_Code_9_SRP9 { get; set; }
        public string Location { get; set; }
        public string Purchasing_Taxable { get; set; }
    }

    public class ItemPriceUpdate
    {
        public string LongItemNumber { get; set; }
        public List<ItemPrice> GridArray { get; set; }
    }

    public class ItemPrice
    {
        public string UM { get; set; }
        public string Unit_Price { get; set; }
        public string Eff_Date_From { get; set; }
        public string Eff_Date_Thru { get; set; }
        public string Currency_Code { get; set; }
        public string Branch_Plant { get; set; }
    }

    public class PriceUpdate
    {
        public List<PricetItem> PriceArray { get; set; }
    }

    public class PricetItem
    {
        public int mnDocumentOrderInvoiceE { get; set; }
        public string szOrderType { get; set; }
        public string szCompanyKeyOrderNo { get; set; }
        public int mnLineNumber { get; set; }
        public int mnSequenceNumber { get; set; }
        public string szPriceAdjustmentScheduleN { get; set; }
        public string szPriceAdjustmentType { get; set; }
        public int mnIdentifierShortItem { get; set; }
        public int mnAddressNumber { get; set; }
        public string szCurrencyCodeFrom { get; set; }
        public string szUnitOfMeasureAsInput { get; set; }
        public int mnQuantityMinimum { get; set; }
        public string cBasisCode { get; set; }
        public string mnFactorValue { get; set; }
        public string cAdjustmentBasedon { get; set; }
        public double mnAmtPricePerUnit { get; set; }
        public double mnAmtPricePerUnit2 { get; set; }
        public double mnAmtForPricePerUnit { get; set; }
        public string szGlClass { get; set; }
        public string szAdjustmentReasonCode { get; set; }
        public string cAdjustmentControlCode { get; set; }
        public string cManualDiscount { get; set; }
        public string cPriceOverrideCode { get; set; }
        public string cOrderLevelAdjustmentYN { get; set; }
        public string cMutuallyExclusiveAdjustme { get; set; }
        public string cPromotionDisplayControl { get; set; }
        public string szUserId { get; set; }
        public string szProgramId { get; set; }
        public string szWorkStationId { get; set; }
        public string jdDateUpdated { get; set; }
        public int mnTimeOfDay { get; set; }
        public string cNewBasePriceFlag { get; set; }
        public string szDescriptionLine1 { get; set; }
        public string szErrorDescription { get; set; }
        public int mnTier { get; set; }
    }

    public class OrderHeader
    {
        public string cEDIType { get; set; }
        public string szCompanyKeyEdiOrder { get; set; }
        public string mnEdiDocumentNumber { get; set; }
        public string szEdiDocumentType { get; set; }
        public string mnEdiLineNumber { get; set; }
        public string szEdiTransactionSet { get; set; }
        public string jdEdiTransmissionDate { get; set; }
        public string cEdiSendRcvIndicator { get; set; }
        public string cEdiSuccessfullyProcess { get; set; }
        public string szEdiBatchNumber { get; set; }
        public string szCompanyKeyOrderNo { get; set; }
        public string mnDocumentOrderInvoiceE { get; set; }
        public string szOrderType { get; set; }
        public string szCostCenter { get; set; }
        public string szCompany { get; set; }
        public string mnAddressNumber { get; set; }
        public string mnAddressNumberShipTo { get; set; }
        public string mnAddressNumberParent { get; set; }
        public string jdDateRequestedJulian { get; set; }
        public string jdDateTransactionJulian { get; set; }
        public string szReference1 { get; set; }
        public string szDeliveryInstructLine1 { get; set; }
        public string szDeliveryInstructLine2 { get; set; }
        public string szRouteCode { get; set; }
        public string szFreightHandlingCode { get; set; }
        public string mnCarrier { get; set; }
        public string szModeOfTransport { get; set; }
        public string szZoneNumber { get; set; }
        public string cCurrencyMode { get; set; }
        public string szOrderedBy { get; set; }
        public string szOrderTakenBy { get; set; }
        public string szUserReservedReference { get; set; }
        public string szTransactionOriginator { get; set; }
        public string szProgramId { get; set; }
        public string szUserId { get; set; }
        public string jdDateUpdated { get; set; }
        public string mnTimeOfDay { get; set; }
        public string nSourceOfOrder { get; set; }
        public string szIntegrationReference01 { get; set; }
        public string szIntegrationReference02 { get; set; }
        public string szIntegrationReference03 { get; set; }
        public string szIntegrationReference04 { get; set; }
        public string szIntegrationReference05 { get; set; }
        public string szHoldOrdersCode { get; set; }
        public string szUserReservedCode { get; set; }
        public string mnUserReservedAmount { get; set; }
        public string szPriceAdjustmentScheduleN { get; set; }
        public string szErrorDescription { get; set; }
    }

    public class OrderHeaderReturn
    {
        public string sz55ErrorDescription { get; set; }
    }

    public class OrderDetailReturn
    {
        public ServiceRequest ServiceRequest1 { get; set; }
    }

    public class ServiceRequest
    {
        public string name { get; set; }
        public string template { get; set; }
        public bool submitted { get; set; }
        public Result result { get; set; }
    }

    public class Result
    {
        public string status { get; set; }
        public string message { get; set; }
        public List<Output> output { get; set; }
    }

    public class Output
    {
        public int id { get; set; }
        public string value { get; set; }
        public string name { get; set; }
    }

    public class OrderDetail
    {
        public List<DetailLine> F47012 { get; set; }
    }
    public class DetailLine
    {
        public string cEDIType { get; set; }
        public string szCompanyKeyEdiOrder { get; set; }
        public string mnEdiDocumentNumber { get; set; }
        public string szEdiDocumentType { get; set; }
        public string mnEdiLineNumber { get; set; }
        public string szEdiTransactionSet { get; set; }
        public string jdEdiTransmissionDate { get; set; }
        public string cEdiSendRcvIndicator { get; set; }
        public string cEdiSuccessfullyProcess { get; set; }
        public string szEdiBatchNumber { get; set; }
        public string szCompanyKeyOrderNo { get; set; }
        public string mnDocumentOrderInvoiceE { get; set; }
        public string szOrderType { get; set; }
        public string mnLineNumber { get; set; }
        public string szCostCenter { get; set; }
        public string szCompany { get; set; }
        public string mnAddressNumber { get; set; }
        public string mnAddressNumberShipTo { get; set; }
        public string mnAddressNumberParent { get; set; }
        public string jdDateRequestedJulian { get; set; }
        public string jdDateTransactionJulian { get; set; }
        public string szReference1 { get; set; }
        public string szIdentifier2ndItem { get; set; }
        public string szLot { get; set; }
        public string szDescriptionLine1 { get; set; }
        public string szDescriptionLine2 { get; set; }
        public string szLineType { get; set; }
        public string szReference2Vendor { get; set; }
        public string mnLineNumberKitMaster { get; set; }
        public string mnComponentNumber { get; set; }
        public string mnRelatedKitComponent { get; set; }
        public string mnUnitsTransactionQty { get; set; }
        public string mnAmtListPricePerUnit { get; set; }
        public string mnAmtPricePerUnit2 { get; set; }
        public string mnAmountExtendedPrice { get; set; }
        public string mnAmtForPricePerUnit { get; set; }
        public string mnAmountForeignExtPrice { get; set; }
        public string mnAmountListPriceForeign { get; set; }
        public string mnAmountForeignUnitCost { get; set; }
        public string mnAmountForeignExtCost { get; set; }
        public string mnCurrencyConverRateOv { get; set; }
        public string szCurrencyCodeFrom { get; set; }
        public string mnAmountUnitWeight { get; set; }
        public string cPriceOverrideCode { get; set; }
        public string cTemporaryPriceYN { get; set; }
        public string cTaxableYN { get; set; }
        public string szRouteCode { get; set; }
        public string szFreightHandlingCode { get; set; }
        public string mnCarrier { get; set; }
        public string szModeOfTransport { get; set; }
        public string mnCentury { get; set; }
        public string cWoOrderFreezeCode { get; set; }
        public string mnUserReservedAmount { get; set; }
        public string szTransactionOriginator { get; set; }
        public string szProgramId { get; set; }
        public string szUserId { get; set; }
        public string jdDateUpdated { get; set; }
        public string mnTimeOfDay { get; set; }
        public string nSourceOfOrder { get; set; }
        public string szIntegrationReference01 { get; set; }
        public string szIntegrationReference02 { get; set; }
        public string szIntegrationReference03 { get; set; }
        public string szIntegrationReference04 { get; set; }
        public string szIntegrationReference05 { get; set; }
        public string cWOItmNbr { get; set; }
        public string szCategoriesWorkOrder001 { get; set; }
        public string szCategoriesWorkOrder002 { get; set; }
        public string szCategoriesWorkOrder003 { get; set; }
        public string szCategoriesWorkOrder004 { get; set; }
        public string szCategoriesWorkOrder005 { get; set; }
        public string szCategoriesWorkOrder006 { get; set; }
        public string szCategoriesWorkOrder007 { get; set; }
        public string szCategoriesWorkOrder008 { get; set; }
        public string szCategoriesWorkOrder009 { get; set; }
        public string szCategoriesWorkOrder010 { get; set; }
        public string szProgramId_2 { get; set; }
        public int mnTimeOfDay_2 { get; set; }
        public int mnUserReservedNumber { get; set; }
        public int mnNumericField01 { get; set; }
        public int mnNumericField02 { get; set; }
        public int mnNumericField03 { get; set; }
        public int mnNumericField04 { get; set; }
        public int mnNumericField05 { get; set; }
        public string szStringField01 { get; set; }
        public string szStringField02 { get; set; }
        public string szStringField03 { get; set; }
        public string szStringField04 { get; set; }
        public string szStringField05 { get; set; }
        public string szErrorDescription { get; set; }
        public string Szir05 { get; set; }
    }

    public class BillOfMaterial
    {
        public List<GridData> GridData_1 { get; set; }
        public string Parent_ItemNbr { get; set; }
        public string Parent_Branch { get; set; }
    }

    public class GridData
    {
        public string Quantity { get; set; }
        public string Item_NumberG { get; set; }
    }

    public class JDERouting
    {
        public string Branch { get; set; }
        public string Item_Number { get; set; }
        public List<RoutingDetail> RoutingDetail { get; set; }
        public string P3003_Version { get; set; }
    }

    public class RoutingDetail
    {
        public string Work_Center { get; set; }
        public string Oper_Seq { get; set; }
        public string Run_Labor { get; set; }
    }

    public class JDERoutingV2
    {
        public string Branch { get; set; }
        public string Item_Number { get; set; }
        public List<RoutingDetailV2> RoutingDetail { get; set; }
        public string P3003_Version { get; set; }
    }

    public class RoutingDetailV2
    {
        public string Work_Center { get; set; }
        public string Oper_Seq { get; set; }
        public string Run_Labor { get; set; }
        public int Supplier { get; set; }
        public string Cost_Type { get; set; }
        public string PO_YN { get; set; }
        public string Time_Basis { get; set; }
    }

    public class UpdateOrder
    {
        public string ORDER_NUMBER { get; set; }
        public string REFERENCE_NUMBER { get; set; }
        public string STATUS { get; set; }
    }

    public class TextAttachment
    {
        public string moStructure { get; set; }
        public string[] moKey { get; set; }
        public string formName { get; set; }
        public string version { get; set; }
        public string itemName { get; set; }
        public string inputText { get; set; }
    }

    public class MoKey1
    {
        public string SalesOrder { get; set; }
        public string SalesOrderType { get; set; }
        public string Company { get; set; }
        public string LineNum { get; set; }
    }

    public class MoKey2
    {
        public string ShortItemNum { get; set; }
        public string BranchPlant { get; set; }
        public string RoutingTypeM { get; set; }
        public string BatchQty { get; set; }
        public string OperationSeq { get; set; }
        public string LineCell { get; set; }
        public string OperationTypeCode { get; set; }
        public string EffectiveFromDate { get; set; }
    }

    public class LotNumberReturn
    {
        public int mnNextNumber001 { get; set; }
        public string szString30A { get; set; }

    }

    public class UnitOfMesaure
    {
        public string Item_Number { get; set; }
        public List<UOMDetail> GridData_1 { get; set; }
    }

    public class UOMDetail
    {
        public string From_UoM { get; set; }
        public string Quantity { get; set; }
        public string To_UoM { get; set; }
    }

    public class CostRollAndFreeze
    {
        public List<CRFDetail> ItemNbrArray { get; set; }
    }

    public class CRFDetail
    {
        public string Item_Nbr { get; set; }
    }

    public class ItemCost
    {
        public List<ItemCostDetail> F4105Z1 { get; set; }
    }

    public class ItemCostDetail
    {
        public string szEdiUserId { get; set; }
        public string szEdiBatchNumber { get; set; }
        public string szEdiTransactNumber { get; set; }
        public int mnEdiLineNumber { get; set; }
        public string cDirectionIndicator { get; set; }
        public string cEdiSuccessfullyProcess { get; set; }
        public string szTransactionAction { get; set; }
        public string szIdentifier2ndItem { get; set; }
        public string szCostCenter { get; set; }
        public string szLedgType { get; set; }
        public decimal mnAmountUnitCost { get; set; }
        public string cCostingSelectionPurchasi { get; set; }
        public string cCostingSelectionInventor { get; set; }
        public string szUserId { get; set; }
        public string szProgramId { get; set; }
        public string szWorkStationId { get; set; }
        public string jdDateUpdated { get; set; }
        public int mnTimeOfDay { get; set; }
        public string szErrorDescription { get; set; }
    }

    [Serializable]
    public class AccessToken
    {
        public string username { get; set; }
        public string environment { get; set; }
        public string role { get; set; }
        public string jasserver { get; set; }
        public Userinfo userInfo { get; set; }
        public bool userAuthorized { get; set; }
        public object version { get; set; }
        public object poStringJSON { get; set; }
        public object altPoStringJSON { get; set; }
        public string aisSessionCookie { get; set; }
        public bool adminAuthorized { get; set; }
        public bool passwordAboutToExpire { get; set; }
        public string envColor { get; set; }
        public string machineName { get; set; }

    }

    [Serializable]
    public class Userinfo
    {
        public string token { get; set; }
        public string langPref { get; set; }
        public string locale { get; set; }
        public string dateFormat { get; set; }
        public string dateSeperator { get; set; }
        public string simpleDateFormat { get; set; }
        public string timeFormat { get; set; }
        public string decimalFormat { get; set; }
        public int addressNumber { get; set; }
        public string alphaName { get; set; }
        public string appsRelease { get; set; }
        public string country { get; set; }
        public string username { get; set; }
        public string longUserId { get; set; }
        public string timeZoneOffset { get; set; }
        public string dstRuleKey { get; set; }
        public Dstrule dstRule { get; set; }
    }

    [Serializable]
    public class Dstrule
    {
        public int startDate { get; set; }
        public int endDate { get; set; }
        public int startTime { get; set; }
        public int endTime { get; set; }
        public int endDay { get; set; }
        public int dstSavings { get; set; }
        public int startDay { get; set; }
        public int endMonth { get; set; }
        public int effectiveYear { get; set; }
        public int dstruleOffset { get; set; }
        public int startMonth { get; set; }
        public int endDayOfWeek { get; set; }
        public object ruleDescription { get; set; }
        public int startDayOfWeek { get; set; }
        public object startEffectiveDate { get; set; }
        public object endEffectiveDate { get; set; }
    }

    [Serializable]
    public class PostOrder
    {
        public string Company { get; set; }
        public string OrderNbr { get; set; }
        public string DocType { get; set; }
        public string YGroup_IR03 { get; set; }
        public string Branch { get; set; }
        public string InitialParentItemNbr { get; set; }
        public string Order_Quantity { get; set; }
    }
}
