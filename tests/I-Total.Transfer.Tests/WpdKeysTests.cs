using System.Reflection;
using ITotalTransfer.Devices.Wpd;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITotalTransfer.Tests;

/// <summary>
/// The WPD constants are copied from the Windows Portable Devices contract, and a wrong GUID fails in the
/// quietest possible way: the device simply never matches the branch that reads it. That happened to
/// <c>WPD_FUNCTIONAL_CATEGORY_STORAGE</c>, whose last digits were mistyped, so every Android volume looked
/// like a file while the iPhone path kept working. The values are pinned here so a future edit has to be
/// deliberate.
/// </summary>
[TestClass]
public sealed class WpdKeysTests
{
    [TestMethod]
    public void StorageFunctionalCategoryMatchesTheWindowsContract()
    {
        Assert.AreEqual(new Guid("23F05BBC-15DE-4C2A-A55B-A9AF5CE412EF"), ReadGuid("FunctionalCategoryStorage"));
    }

    [TestMethod]
    public void FolderContentTypeMatchesTheWindowsContract()
    {
        Assert.AreEqual(new Guid("27E2E392-A111-48E0-AB0C-E17705A05F85"), ReadGuid("ContentTypeFolder"));
    }

    private static Guid ReadGuid(string fieldName)
    {
        Type? keys = typeof(WpdDevice).Assembly.GetType("ITotalTransfer.Devices.Wpd.WpdKeys");
        Assert.IsNotNull(keys, "WpdKeys is part of the WPD interop and must exist.");
        FieldInfo? field = keys.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, fieldName + " must exist.");
        return (Guid)field.GetValue(null)!;
    }
}
