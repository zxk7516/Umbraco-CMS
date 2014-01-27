using System;

namespace umbraco
{
    // fixme - should move to LegacyClasses! (later)
    [Obsolete("Use an Attempt<T> instead!", false)]
	public class ScriptingMacroResult
	{
		public ScriptingMacroResult()
		{
		}

		public ScriptingMacroResult(string result, Exception resultException)
		{
			Result = result;
			ResultException = resultException;
		}

		public string Result { get; set; }
		public Exception ResultException { get; set; }
	}
}