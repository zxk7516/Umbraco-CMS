namespace umbraco.cms.businesslogic.macro
{
    // indicates the type of a macro
	public enum MacroTypes
	{
		XSLT = 1,
		CustomControl = 2,
		UserControl = 3,
		Unknown = 4,
		Python = 5,
		Script = 6,
		PartialView = 7
	}
}