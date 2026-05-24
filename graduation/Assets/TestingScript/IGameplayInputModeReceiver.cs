public interface IGameplayInputModeReceiver
{
    /// <summary>
    /// Enables or disables local gameplay input without disabling movement motors or network components.
    /// </summary>
    void SetGameplayInputEnabled(bool enabledValue);
}
