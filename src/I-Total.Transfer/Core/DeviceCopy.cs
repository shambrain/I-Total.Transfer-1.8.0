namespace ITotalTransfer.Core;

/// <summary>
/// The words the interface uses for the attached handset.
///
/// <para>
/// The application reads two families through one engine, so a sentence that names the iPhone in front
/// of an Android phone is simply wrong - and it is the kind of wrong that makes an operator think the
/// app has picked up the other device. Keeping the noun in one place means the rule is testable and a
/// new sentence cannot quietly reintroduce "iPhone" for everyone.
/// </para>
/// </summary>
public static class DeviceCopy
{
    /// <summary>The handset without an article: "iPhone", "Android phone", or the neutral "phone".</summary>
    public static string Noun(DeviceFamily family) => family switch
    {
        DeviceFamily.Android => "Android phone",
        DeviceFamily.Apple => "iPhone",
        _ => "phone"
    };

    /// <summary>The handset ready to follow a verb or preposition: "the iPhone", "the Android phone".</summary>
    public static string WithArticle(DeviceFamily family) => family switch
    {
        DeviceFamily.Android => "the Android phone",
        DeviceFamily.Apple => "the iPhone",
        _ => "the phone"
    };
}
