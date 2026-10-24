using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ITotalTransfer.Devices.Afc;

/// <summary>AFC status codes (afc.h).</summary>
internal enum AfcStatus
{
    Success = 0,
    UnknownError = 1,
    OpHeaderInvalid = 2,
    NoResources = 3,
    ReadError = 4,
    WriteError = 5,
    UnknownPacketType = 6,
    InvalidArg = 7,
    ObjectNotFound = 8,
    ObjectIsDir = 9,
    PermDenied = 10,
    ServiceNotConnected = 11,
    OpTimeout = 12,
    TooMuchData = 13,
    EndOfData = 14,
    OpNotSupported = 15,
    ObjectExists = 16,
    ObjectBusy = 17,
    NoSpaceLeft = 18,
    OpWouldBlock = 19,
    IoError = 20,
    OpInterrupted = 21,
    OpInProgress = 22,
    InternalError = 23,
    MuxError = 30,
    NoMem = 31,
    NotEnoughData = 32,
    DirNotEmpty = 33
}

/// <summary>
/// P/Invoke surface for libimobiledevice + AFC, taken from the public headers
/// (libimobiledevice/libimobiledevice.h, /lockdown.h, /afc.h). The library is only loaded after the
/// binaries have been verified against the dependency manifest; a missing or unverified runtime
/// surfaces as <see cref="DllNotFoundException"/> which the backend converts into "unavailable".
/// </summary>
internal static class AfcNative
{
    private const string Lib = "libimobiledevice-1.0.dll";
    private const string Crt = "msvcrt.dll";

    /// <summary>UTF-8 (not UTF-16) marshalling is required for every libimobiledevice path string.</summary>
    private const CharSet Char = CharSet.Ansi;

    private static readonly object RuntimeGate = new();
    private static volatile string? _verifiedDirectory;
    private static bool _resolverInstalled;

    /// <summary>
    /// Loads only the binaries that <see cref="DependencyVerifier"/> verified and binds future
    /// P/Invokes in this assembly to that directory. Without this, a verified file in
    /// <c>native\</c> could still be bypassed by a same-named DLL earlier on the Windows search path.
    /// Dependencies are loaded first; a retry pass handles a manifest whose order is not topological.
    /// </summary>
    internal static void ConfigureVerifiedRuntime(string directory, IReadOnlyList<string> verifiedFileNames)
    {
        lock (RuntimeGate)
        {
            string fullDirectory = Path.GetFullPath(directory);
            if (_verifiedDirectory is not null &&
                !string.Equals(_verifiedDirectory, fullDirectory, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "A different AFC native runtime is already loaded in this process; mixing two runtime directories is refused.");

            var pending = verifiedFileNames
                .Select(name => Path.GetFullPath(Path.Combine(fullDirectory, name)))
                .ToList();
            var failures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (pending.Count > 0)
            {
                int loaded = 0;
                var retry = new List<string>();
                foreach (string path in pending)
                {
                    try
                    {
                        NativeLibrary.Load(path);
                        loaded++;
                    }
                    catch (Exception ex)
                    {
                        failures[path] = ex.Message;
                        retry.Add(path);
                    }
                }
                if (loaded == 0)
                {
                    string first = retry[0];
                    throw new InvalidOperationException(
                        $"The verified native file '{Path.GetFileName(first)}' could not be loaded: {failures[first]}");
                }
                pending = retry;
            }

            _verifiedDirectory = fullDirectory;
            if (!_resolverInstalled)
            {
                NativeLibrary.SetDllImportResolver(typeof(AfcNative).Assembly, ResolveVerifiedLibrary);
                _resolverInstalled = true;
            }
        }
    }

    private static IntPtr ResolveVerifiedLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        // The CRT is a Windows component, not part of the pinned runtime.
        if (string.Equals(libraryName, Crt, StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        string? directory = _verifiedDirectory;
        if (string.IsNullOrWhiteSpace(directory))
            throw new DllNotFoundException("No verified AFC native runtime is configured.");

        string candidate = Path.Combine(directory, libraryName);
        if (!File.Exists(candidate))
            throw new DllNotFoundException($"'{libraryName}' is not present in the verified AFC runtime directory.");
        return NativeLibrary.Load(candidate);
    }

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int idevice_get_device_list(out IntPtr devices, out int count);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int idevice_device_list_free(IntPtr devices);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int idevice_new(out IntPtr device, [MarshalAs(UnmanagedType.LPUTF8Str)] string? udid);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void idevice_free(IntPtr device);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int lockdownd_client_new_with_handshake(IntPtr device, out IntPtr client, [MarshalAs(UnmanagedType.LPUTF8Str)] string label);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void lockdownd_client_free(IntPtr client);

    // NB: returns a lockdownd_service_descriptor_t* (a struct), which must be freed with the
    // descriptor-specific free — not treated as an opaque AFC handle.
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int lockdownd_start_service(IntPtr client, [MarshalAs(UnmanagedType.LPUTF8Str)] string identifier, out IntPtr service);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void lockdownd_service_descriptor_free(IntPtr service);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int afc_client_new(IntPtr device, IntPtr service, out IntPtr client);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int afc_client_start_service(IntPtr device, out IntPtr client, [MarshalAs(UnmanagedType.LPUTF8Str)] string label);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int afc_client_free(IntPtr client);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int afc_read_directory(IntPtr client, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr info);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int afc_get_file_info(IntPtr client, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr info);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int afc_open_file(IntPtr client, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, ulong fileMode, out IntPtr handle);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int afc_read_file(IntPtr client, IntPtr handle, byte[] data, int length, out int bytes);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int afc_write_file(IntPtr client, IntPtr handle, byte[] data, int length, out int bytes);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int afc_close_file(IntPtr client, IntPtr handle);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int afc_file_seek(IntPtr client, IntPtr handle, long offset, int whence);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int afc_make_directory(IntPtr client, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int afc_remove_path(IntPtr client, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int afc_rename_path(IntPtr client, [MarshalAs(UnmanagedType.LPUTF8Str)] string from, [MarshalAs(UnmanagedType.LPUTF8Str)] string to);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int afc_truncate(IntPtr client, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, ulong newsize);

    [DllImport(Crt, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void free(IntPtr pointer);

    /// <summary>AFC_FOPEN_RDONLY.</summary>
    internal const ulong AfcFopenRdOnly = 0x00000001;
    /// <summary>AFC_FOPEN_RW (create + read/write).</summary>
    internal const ulong AfcFopenRw = 0x00000002;
    internal const int SeekSet = 0;

    /// <summary>Reads a NULL-terminated <c>char**</c> list into managed strings and frees it.</summary>
    internal static List<string> ReadCStringList(IntPtr list)
    {
        var result = new List<string>();
        if (list == IntPtr.Zero) return result;
        int i = 0;
        while (true)
        {
            IntPtr entry = Marshal.ReadIntPtr(list, i * IntPtr.Size);
            if (entry == IntPtr.Zero) break;
            result.Add(Marshal.PtrToStringUTF8(entry) ?? "");
            i++;
        }
        // The list, its entries and the strings are allocated by libimobiledevice; free them all.
        IntPtr p = list;
        int j = 0;
        while (true)
        {
            IntPtr entry = Marshal.ReadIntPtr(p, j * IntPtr.Size);
            if (entry == IntPtr.Zero) break;
            free(entry);
            j++;
        }
        free(p);
        return result;
    }

    /// <summary>
    /// AFC directory listings and file properties come back as a flat list of alternating
    /// key/value C strings terminated by two NULLs. Convert to a dictionary.
    /// </summary>
    internal static Dictionary<string, string> ReadKeyValueList(IntPtr list)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (list == IntPtr.Zero) return map;
        var raw = new List<(IntPtr Ptr, string Value)>();
        int i = 0;
        while (true)
        {
            IntPtr entry = Marshal.ReadIntPtr(list, i * IntPtr.Size);
            if (entry == IntPtr.Zero) break;
            raw.Add((entry, Marshal.PtrToStringUTF8(entry) ?? ""));
            i++;
        }
        for (int k = 0; k + 1 < raw.Count; k += 2)
            map[raw[k].Value] = raw[k + 1].Value;
        foreach (var (ptr, _) in raw) free(ptr);
        free(list);
        return map;
    }

    internal static string ToUtf8(byte[] buffer, int length) => Encoding.UTF8.GetString(buffer, 0, length);
}
