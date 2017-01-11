namespace Umbraco.Core.Sync
{
    /// <summary>
    /// The type of Cold Boot taking place
    /// </summary>
    public enum ColdBootType
    {
        /// <summary>
        /// Indicates there was no cold boot
        /// </summary>
        NoColdBoot,

        /// <summary>
        /// This indicates that there is no lasysynced file for the current machine/appid
        /// </summary>
        NeverSynced,

        /// <summary>
        /// This indicates that the number of instructions needing to be processed exceeds the value stored 
        /// for the <see cref="DatabaseServerMessengerOptions.MaxProcessingInstructionCount"/>
        /// </summary>
        ExceedsMaxProcessingInstructionCount
    }
}