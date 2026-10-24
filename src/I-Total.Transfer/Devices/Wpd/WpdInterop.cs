using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ITotalTransfer.Devices.Wpd;

/// <summary>
/// Portable Devices (WPD) COM interop. Interface IIDs, coclass CLSIDs and property keys are taken
/// verbatim from the Windows SDK headers (PortableDeviceApi.h, PortableDeviceTypes.h,
/// PortableDevice.h) so the values are authoritative rather than guessed.
/// </summary>
internal static class WpdGuids
{
    // Coclasses (PortableDeviceApi.h / PortableDeviceTypes.h)
    public static readonly Guid ClsidPortableDeviceManager = new("0af10cec-2ecd-4b92-9581-34f6ae0637f3");
    public static readonly Guid ClsidPortableDevice = new("728a21c5-3d9e-48d7-9810-864848f0f404");
    public static readonly Guid ClsidPortableDeviceFTM = new("f7c0039a-4762-488a-b4b3-760ef9a1ba9b");
    public static readonly Guid ClsidPortableDeviceValues = new("0c15d503-d017-47ce-9016-7b3f978721cc");
    public static readonly Guid ClsidPortableDeviceKeyCollection = new("de2d022d-2480-43be-97f0-d1fa2cf98f4f");
}

internal static class WpdKeys
{
    public const string DeviceObjectId = "DEVICE";
    public const uint WpdFlagNone = 0;
    public const uint StgmRead = 0x00000000;             // STGM_READ
    public const uint WpdResourceDevice = 0;

    public static readonly PropertyKey DeviceFriendlyName = new(new Guid("26D4979A-E643-4626-9E2B-736DC0C92FDC"), 12);
    public static readonly PropertyKey DeviceModel = new(new Guid("26D4979A-E643-4626-9E2B-736DC0C92FDC"), 8);
    public static readonly PropertyKey DeviceSerialNumber = new(new Guid("26D4979A-E643-4626-9E2B-736DC0C92FDC"), 9);
    public static readonly PropertyKey DeviceFirmwareVersion = new(new Guid("26D4979A-E643-4626-9E2B-736DC0C92FDC"), 3);
    public static readonly PropertyKey DeviceManufacturer = new(new Guid("26D4979A-E643-4626-9E2B-736DC0C92FDC"), 7);

    public static readonly PropertyKey ObjectId = new(new Guid("EF6B490D-5CD8-437A-AFFC-DA8B60EE4A3C"), 2);
    public static readonly PropertyKey ObjectName = new(new Guid("EF6B490D-5CD8-437A-AFFC-DA8B60EE4A3C"), 4);
    public static readonly PropertyKey ObjectOriginalFileName = new(new Guid("EF6B490D-5CD8-437A-AFFC-DA8B60EE4A3C"), 12);
    public static readonly PropertyKey ObjectSize = new(new Guid("EF6B490D-5CD8-437A-AFFC-DA8B60EE4A3C"), 11);
    public static readonly PropertyKey ObjectContentType = new(new Guid("EF6B490D-5CD8-437A-AFFC-DA8B60EE4A3C"), 7);
    public static readonly PropertyKey ObjectDateModified = new(new Guid("EF6B490D-5CD8-437A-AFFC-DA8B60EE4A3C"), 19);
    public static readonly PropertyKey FunctionalObjectCategory = new(new Guid("8F052D93-ABCA-4FC5-A5AC-B01DF4DBE598"), 2);

    public static readonly PropertyKey ResourceDefault = new(new Guid("E81E79BE-34F0-41BF-B53F-F1A06AE87842"), 0);

    public static readonly Guid ContentTypeFolder = new("27E2E392-A111-48E0-AB0C-E17705A05F85");
    // Windows Portable Devices contract: WPD_FUNCTIONAL_CATEGORY_STORAGE is
    // {23F05BBC-15DE-4C2A-A55B-A9AF5CE412EF}. The last twelve digits were mistyped here, which made
    // every Android storage volume fail the "is this a container?" test while the iPhone (a folder
    // content type) kept working - so Android support was inert on real hardware until this was fixed.
    public static readonly Guid FunctionalCategoryStorage = new("23F05BBC-15DE-4C2A-A55B-A9AF5CE412EF");

    // Client information keys (WPD_CLIENT_*)
    public static readonly PropertyKey ClientName = new(new Guid("204D9F0C-2292-4080-9F42-40664E70F859"), 2);
    public static readonly PropertyKey ClientMajorVersion = new(new Guid("204D9F0C-2292-4080-9F42-40664E70F859"), 3);
    public static readonly PropertyKey ClientMinorVersion = new(new Guid("204D9F0C-2292-4080-9F42-40664E70F859"), 4);
    public static readonly PropertyKey ClientRevision = new(new Guid("204D9F0C-2292-4080-9F42-40664E70F859"), 5);
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct PropertyKey
{
    public Guid FormatId;
    public uint PropertyId;
    public PropertyKey(Guid formatId, uint propertyId) { FormatId = formatId; PropertyId = propertyId; }
}

[ComImport, Guid("a1567595-4c2f-4574-a6fa-ecef917b9a40"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPortableDeviceManager
{
    // HRESULT GetDevices([in,out] LPWSTR* pPnPDeviceIDs, [in,out] DWORD* pcPnPDeviceIDs)
    void GetDevices(IntPtr pPnPDeviceIDs, ref uint pcPnPDeviceIDs);
    void RefreshDeviceList();
    void GetDeviceFriendlyName([MarshalAs(UnmanagedType.LPWStr)] string pszPnPDeviceID, IntPtr pDeviceFriendlyName, ref uint pcchDeviceFriendlyName);
    void GetDeviceDescription([MarshalAs(UnmanagedType.LPWStr)] string pszPnPDeviceID, IntPtr pDeviceDescription, ref uint pcchDeviceDescription);
    void GetDeviceManufacturer([MarshalAs(UnmanagedType.LPWStr)] string pszPnPDeviceID, IntPtr pDeviceManufacturer, ref uint pcchDeviceManufacturer);
    void GetDeviceProperty([MarshalAs(UnmanagedType.LPWStr)] string pszPnPDeviceID, IntPtr pszDevicePropertyName, IntPtr pData, ref uint pcbData);
    void GetPrivateDevices(IntPtr pPnPDeviceIDs, ref uint pcPnPDeviceIDs);
}

[ComImport, Guid("625e2df8-6392-4cf0-9ad1-3cfa5f17775c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPortableDevice
{
    void Open([MarshalAs(UnmanagedType.LPWStr)] string pszPnPDeviceID, IPortableDeviceValues? pClientInfo);
    void SendCommand(uint dwFlags, IPortableDeviceValues pParameters, out IPortableDeviceValues ppResults);
    void Content(out IPortableDeviceContent ppContent);
    void Capabilities(out IntPtr ppCapabilities);
    void Cancel();
    void Close();
    void Advise(uint dwFlags, IntPtr pCallback, IntPtr pParameters);
    void Unadvise(IntPtr pCallback);
    void GetPnPDeviceID(out IntPtr ppszPnPDeviceID);
}

[ComImport, Guid("6a96ed84-7c73-4480-9938-bf5af477d426"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPortableDeviceContent
{
    void EnumObjects(uint dwFlags, [MarshalAs(UnmanagedType.LPWStr)] string? pszParentObjectID, IntPtr pFilter, out IEnumPortableDeviceObjectIDs ppEnum);
    void Properties(out IPortableDeviceProperties ppProperties);
    void Transfer(out IPortableDeviceResources ppResources);
    void CreateObjectWithPropertiesOnly(IPortableDeviceValues pValues, ref IntPtr ppszObjectID);
    void CreateObjectWithPropertiesAndData(IPortableDeviceValues pValues, out IStream ppData, ref uint pdwOptimalWriteBufferSize, IntPtr ppszCookie);
    void Delete([MarshalAs(UnmanagedType.LPWStr)] string pszObjectID, IPortableDeviceKeyCollection? pKeys);
    void GetObjectIDsFromPersistentUniqueIDs(IntPtr pPersistentUniqueIDs, out IEnumPortableDeviceObjectIDs ppObjects);
    void Cancel();
    void Move(IntPtr pObjects, [MarshalAs(UnmanagedType.LPWStr)] string pszDestinationFolderObjectID);
    void Copy(IntPtr pObjects, [MarshalAs(UnmanagedType.LPWStr)] string pszDestinationFolderObjectID);
}

[ComImport, Guid("10ece955-cf41-4728-bfa0-41eedf1bbf19"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IEnumPortableDeviceObjectIDs
{
    // HRESULT Next([in] ULONG cObjects, [out] LPWSTR* pObjIDs, [out] ULONG* pcFetched)
    void Next(uint cObjects, IntPtr pObjIDs, out uint pcFetched);
    void Skip(uint cObjects);
    void Reset();
    void Clone(out IEnumPortableDeviceObjectIDs ppEnum);
    void Cancel();
}

[ComImport, Guid("7f6d695c-03df-4439-a809-59266beee3a6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPortableDeviceProperties
{
    void GetSupportedProperties([MarshalAs(UnmanagedType.LPWStr)] string pszObjectID, out IPortableDeviceKeyCollection ppKeys);
    void GetPropertyAttributes([MarshalAs(UnmanagedType.LPWStr)] string pszObjectID, IPortableDeviceKeyCollection pKeys, out IPortableDeviceValues ppAttributes);
    void GetValues([MarshalAs(UnmanagedType.LPWStr)] string pszObjectID, IPortableDeviceKeyCollection? pKeys, out IPortableDeviceValues ppValues);
    void SetValues([MarshalAs(UnmanagedType.LPWStr)] string pszObjectID, IPortableDeviceValues pValues, out IPortableDeviceValues ppResults);
    void Delete([MarshalAs(UnmanagedType.LPWStr)] string pszObjectID, IPortableDeviceKeyCollection pKeys);
    void Cancel();
}

[ComImport, Guid("fd8878ac-d841-4d17-891c-e6829cdb6934"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPortableDeviceResources
{
    void GetSupportedResources([MarshalAs(UnmanagedType.LPWStr)] string pszObjectID, out IPortableDeviceKeyCollection ppKeys);
    void GetResourceAttributes([MarshalAs(UnmanagedType.LPWStr)] string pszObjectID, ref PropertyKey Key, out IPortableDeviceValues ppResourceAttributes);
    void GetStream([MarshalAs(UnmanagedType.LPWStr)] string pszObjectID, ref PropertyKey Key, uint dwMode, ref uint pdwOptimalBufferSize, out IStream ppStream);
    void Delete([MarshalAs(UnmanagedType.LPWStr)] string pszObjectID, ref PropertyKey Key);
    void Cancel();
    void CreateResource(IntPtr pResourceAttributes, out IStream ppStream, ref uint pdwOptimalWriteBufferSize, IntPtr ppszCookie);
}

[ComImport, Guid("dada2357-e0ad-492e-98db-dd61c53ba353"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPortableDeviceKeyCollection
{
    void GetCount(out uint pnCount);
    void GetAt(uint nIndex, out PropertyKey pKey);
    void Add(ref PropertyKey pKey);
    void Clear();
    void RemoveAt(uint nIndex);
}

/// <summary>
/// IPortableDeviceValues. Only the methods this app calls are used; earlier vtable slots are declared
/// in order so the CLR maps them to the correct COM slots (unknown slots use IntPtr placeholders).
/// </summary>
[ComImport, Guid("6848f6f2-3155-4f86-b6f5-263eeeab3143"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPortableDeviceValues
{
    void GetCount(out uint pnCount);                                                                          // 1
    void GetAt(uint nIndex, out PropertyKey pKey, IntPtr pValue);                                             // 2
    void SetValue(ref PropertyKey pKey, IntPtr pValue);                                                        // 3
    void GetValue(ref PropertyKey pKey, IntPtr pValue);                                                        // 4
    void SetStringValue(ref PropertyKey pKey, [MarshalAs(UnmanagedType.LPWStr)] string pValue);               // 5
    void GetStringValue(ref PropertyKey pKey, out IntPtr pValue, out uint pcchValue);                          // 6
    void SetUnsignedIntegerValue(ref PropertyKey pKey, uint pValue);                                           // 7
    void GetUnsignedIntegerValue(ref PropertyKey pKey, out uint pValue);                                       // 8
    void SetSignedIntegerValue(ref PropertyKey pKey, int pValue);                                              // 9
    void GetSignedIntegerValue(ref PropertyKey pKey, out int pValue);                                          // 10
    void SetUnsignedLargeIntegerValue(ref PropertyKey pKey, ulong pValue);                                     // 11
    void GetUnsignedLargeIntegerValue(ref PropertyKey pKey, out ulong pValue);                                 // 12
    void SetSignedLargeIntegerValue(ref PropertyKey pKey, long pValue);                                        // 13
    void GetSignedLargeIntegerValue(ref PropertyKey pKey, out long pValue);                                    // 14
    void SetFloatValue(ref PropertyKey pKey, float pValue);                                                    // 15
    void GetFloatValue(ref PropertyKey pKey, out float pValue);                                                // 16
    void SetErrorValue(ref PropertyKey pKey, uint pValue);                                                     // 17
    void GetErrorValue(ref PropertyKey pKey, out uint pValue);                                                 // 18
    void SetKeyValue(ref PropertyKey pKey, ref PropertyKey pValue);                                            // 19
    void GetKeyValue(ref PropertyKey pKey, out PropertyKey pValue);                                            // 20
    void SetBoolValue(ref PropertyKey pKey, int pValue);                                                       // 21
    void GetBoolValue(ref PropertyKey pKey, out int pValue);                                                   // 22
    void SetIUnknownValue(ref PropertyKey pKey, IntPtr pValue);                                                // 23
    void GetIUnknownValue(ref PropertyKey pKey, out IntPtr pValue);                                            // 24
    void SetGuidValue(ref PropertyKey pKey, ref Guid pValue);                                                  // 25
    void GetGuidValue(ref PropertyKey pKey, out Guid pValue);                                                  // 26
}

/// <summary>Factory helpers for the WPD COM classes.</summary>
internal static class WpdFactory
{
    public static IPortableDeviceManager CreateManager() =>
        (IPortableDeviceManager)Activator.CreateInstance(Type.GetTypeFromCLSID(WpdGuids.ClsidPortableDeviceManager, true)!)!;

    /// <summary>Free-threaded (FTM) device object, required for use from an MTA worker thread.</summary>
    public static IPortableDevice CreateDevice()
    {
        try { return (IPortableDevice)Activator.CreateInstance(Type.GetTypeFromCLSID(WpdGuids.ClsidPortableDeviceFTM, true)!)!; }
        catch (COMException) { return (IPortableDevice)Activator.CreateInstance(Type.GetTypeFromCLSID(WpdGuids.ClsidPortableDevice, true)!)!; }
    }

    public static IPortableDeviceValues CreateValues() =>
        (IPortableDeviceValues)Activator.CreateInstance(Type.GetTypeFromCLSID(WpdGuids.ClsidPortableDeviceValues, true)!)!;

    public static IPortableDeviceKeyCollection CreateKeyCollection() =>
        (IPortableDeviceKeyCollection)Activator.CreateInstance(Type.GetTypeFromCLSID(WpdGuids.ClsidPortableDeviceKeyCollection, true)!)!;
}
