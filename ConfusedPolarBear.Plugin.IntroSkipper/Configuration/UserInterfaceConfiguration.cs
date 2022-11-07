namespace ConfusedPolarBear.Plugin.IntroSkipper.Configuration;

/// <summary>
/// User interface configuration.
/// </summary>
public class UserInterfaceConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserInterfaceConfiguration"/> class.
    /// </summary>
    /// <param name="visible">Skip button visibility.</param>
    /// <param name="text">Skip button text.</param>
    public UserInterfaceConfiguration(bool visible, string text)
    {
        SkipButtonVisible = visible;
        SkipButtonText = text;
    }

    /// <summary>
    /// Gets or sets a value indicating whether to show the skip intro button.
    /// </summary>
    public bool SkipButtonVisible { get; set; }

    /// <summary>
    /// Gets or sets the text to display in the skip intro button.
    /// </summary>
    public string SkipButtonText { get; set; }
}
