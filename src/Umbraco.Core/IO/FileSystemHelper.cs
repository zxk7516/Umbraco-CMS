using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;

namespace Umbraco.Core.IO
{
    // NOT USED AT THE MOMENT
    // HERE FOR REFERENCE ONLY

    internal class FileSystemHelper
    {
        // two methods... one uses the normal FileStream constructor which means that
        // everything FileStream does, is done, which is good, but then it relies on
        // exceptions to wait on the file, which is not so cool - the second method
        // does not rely on exceptions but skips a lot of what FileStream constructor
        // would actually do? Though... we're still using that constructor anyway?
        //
        // use the second method
        //
        // OTOH it means we're using PInvoke... might want 'AnotherWay' if we want
        // to run on Linux or?!

        /*
        private static FileStream GetFileStreamAnotherWay(string filePath, FileMode fileMode, FileAccess fileAccess, FileShare fileShare, int timeout)
        {
            var start = DateTime.Now;

            while (true)
            {
                try
                {
                    return new FileStream(filePath, fileMode, fileAccess, fileShare);
                }
                catch (IOException)
                {
                    var errorCode = Marshal.GetLastWin32Error();
                    if (errorCode != ERROR_SHARING_VIOLATION)
                        throw;
                    if (timeout > 0 && (DateTime.Now - start).TotalMilliseconds > timeout)
                        throw;

                    // another way would be to create a filesystem watcher on the file
                    // + an auto reset event, and wait on the event, and trigger the event
                    // when the file changes...

                    Thread.Sleep(100);
                }
            }
        }
        */

        public static FileStream GetFileStream(string filePath, FileMode fileMode, FileAccess fileAccess, FileShare fileShare, int timeout)
        {
            int errorCode;
            var start = DateTime.Now;

            while (true)
            {
                var fileHandle = Win32.CreateFile(filePath, ConvertFileAccess(fileAccess), ConvertFileShare(fileShare),
                    IntPtr.Zero, ConvertFileMode(fileMode), EFileAttributes.Normal, IntPtr.Zero);

                if (fileHandle.IsInvalid == false)
                    return new FileStream(fileHandle, fileAccess /*, bufferSize: 4096, isAsync: true*/);

                errorCode = Marshal.GetLastWin32Error();

                if (errorCode != Win32.ERROR_SHARING_VIOLATION)
                    break;
                if (timeout > 0 && (DateTime.Now - start).TotalMilliseconds > timeout)
                    break;

                Thread.Sleep(100);
            }

            throw new IOException(new Win32Exception(errorCode).Message, errorCode);
        }

        public static void Delete(string filePath, int timeout)
        {
            int errorCode;
            var start = DateTime.Now;

            while (true)
            {
                var r = Win32.DeleteFile(filePath);
                if (r)
                    return;

                errorCode = Marshal.GetLastWin32Error();

                if (errorCode == Win32.ERROR_FILE_NOT_FOUND)
                    return;
                if (errorCode != Win32.ERROR_SHARING_VIOLATION)
                    break;
                if (timeout > 0 && (DateTime.Now - start).TotalMilliseconds > timeout)
                    break;

                Thread.Sleep(100);
            }

            throw new IOException(new Win32Exception(errorCode).Message, errorCode);
        }

        public static async Task<FileStream> GetFileStreamAsync(string filePath, FileMode fileMode, FileAccess fileAccess, FileShare fileShare, int timeout)
        {
            int errorCode;
            var start = DateTime.Now;
            const int defaultBufferSize = 4096; // default FileStream buffer size

            while (true)
            {
                var fileHandle = Win32.CreateFile(filePath, ConvertFileAccess(fileAccess), ConvertFileShare(fileShare),
                    IntPtr.Zero, ConvertFileMode(fileMode), EFileAttributes.Normal, IntPtr.Zero);

                if (fileHandle.IsInvalid == false)
                    return new FileStream(fileHandle, fileAccess, /*bufferSize:*/ defaultBufferSize, /*isAsync:*/ true);

                errorCode = Marshal.GetLastWin32Error();

                if (errorCode != Win32.ERROR_SHARING_VIOLATION)
                    break;
                if (timeout > 0 && (DateTime.Now - start).TotalMilliseconds > timeout)
                    break;

                await Task.Delay(100);
            }

            throw new IOException(new Win32Exception(errorCode).Message, errorCode);
        }

        #region Win32

        // get enum values at
        // http://www.pinvoke.net/default.aspx/kernel32.createfile

        private static EFileAccess ConvertFileAccess(FileAccess fileAccess)
        {
            return fileAccess == FileAccess.ReadWrite
                ? EFileAccess.GenericRead | EFileAccess.GenericWrite
                : (fileAccess == FileAccess.Read
                    ? EFileAccess.GenericRead
                    : EFileAccess.GenericWrite);
        }

        private static EFileShare ConvertFileShare(FileShare fileShare)
        {
            return (EFileShare) ((uint) fileShare);
        }

        private static ECreationDisposition ConvertFileMode(FileMode fileMode)
        {
            return fileMode == FileMode.Open
                ? ECreationDisposition.OpenExisting
                : (fileMode == FileMode.OpenOrCreate
                    ? ECreationDisposition.OpenAlways
                    : (ECreationDisposition)(uint)fileMode);
        }

        [Flags]
        private enum EFileAccess : uint
        {
            // ReSharper disable InconsistentNaming
            // ReSharper disable UnusedMember.Local

            //
            // Standart Section
            //

            AccessSystemSecurity = 0x1000000,   // AccessSystemAcl access type
            MaximumAllowed = 0x2000000,     // MaximumAllowed access type

            Delete = 0x10000,
            ReadControl = 0x20000,
            WriteDAC = 0x40000,
            WriteOwner = 0x80000,
            Synchronize = 0x100000,

            StandardRightsRequired = 0xF0000,
            StandardRightsRead = ReadControl,
            StandardRightsWrite = ReadControl,
            StandardRightsExecute = ReadControl,
            StandardRightsAll = 0x1F0000,
            SpecificRightsAll = 0xFFFF,

            FILE_READ_DATA = 0x0001,        // file & pipe
            FILE_LIST_DIRECTORY = 0x0001,       // directory
            FILE_WRITE_DATA = 0x0002,       // file & pipe
            FILE_ADD_FILE = 0x0002,         // directory
            FILE_APPEND_DATA = 0x0004,      // file
            FILE_ADD_SUBDIRECTORY = 0x0004,     // directory
            FILE_CREATE_PIPE_INSTANCE = 0x0004, // named pipe
            FILE_READ_EA = 0x0008,          // file & directory
            FILE_WRITE_EA = 0x0010,         // file & directory
            FILE_EXECUTE = 0x0020,          // file
            FILE_TRAVERSE = 0x0020,         // directory
            FILE_DELETE_CHILD = 0x0040,     // directory
            FILE_READ_ATTRIBUTES = 0x0080,      // all
            FILE_WRITE_ATTRIBUTES = 0x0100,     // all

            //
            // Generic Section
            //

            GenericRead = 0x80000000,
            GenericWrite = 0x40000000,
            GenericExecute = 0x20000000,
            GenericAll = 0x10000000,

            SPECIFIC_RIGHTS_ALL = 0x00FFFF,
            FILE_ALL_ACCESS =
                StandardRightsRequired |
                Synchronize |
                0x1FF,

            FILE_GENERIC_READ =
                StandardRightsRead |
                FILE_READ_DATA |
                FILE_READ_ATTRIBUTES |
                FILE_READ_EA |
                Synchronize,

            FILE_GENERIC_WRITE =
                StandardRightsWrite |
                FILE_WRITE_DATA |
                FILE_WRITE_ATTRIBUTES |
                FILE_WRITE_EA |
                FILE_APPEND_DATA |
                Synchronize,

            FILE_GENERIC_EXECUTE =
                StandardRightsExecute |
                  FILE_READ_ATTRIBUTES |
                  FILE_EXECUTE |
                  Synchronize

            // ReSharper restore InconsistentNaming
            // ReSharper restore UnusedMember.Local
        }

        [Flags]
        private enum EFileShare : uint
        {
            // ReSharper disable UnusedMember.Local

            None = 0x00000000,
            Read = 0x00000001, // others can read
            Write = 0x00000002, // others can write
            Delete = 0x00000004 // others can delete

            // ReSharper restore UnusedMember.Local
        }

        private enum ECreationDisposition : uint
        {
            // ReSharper disable UnusedMember.Local

            New = 1, // create new, fails if exists
            CreateAlways = 2, // create new, always
            OpenExisting = 3, // open existing, fails if not exists
            OpenAlways = 4, // open, always, create if needed
            TruncateExisting = 5 // open and truncate, fails if not exists

            // ReSharper restore UnusedMember.Local
        }

        [Flags]
        private enum EFileAttributes : uint
        {
            // ReSharper disable UnusedMember.Local

            Readonly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Device = 0x00000040,
            Normal = 0x00000080,
            Temporary = 0x00000100,
            SparseFile = 0x00000200,
            ReparsePoint = 0x00000400,
            Compressed = 0x00000800,
            Offline = 0x00001000,
            NotContentIndexed = 0x00002000,
            Encrypted = 0x00004000,
            WriteThrough = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x08000000,
            DeleteOnClose = 0x04000000,
            BackupSemantics = 0x02000000,
            PosixSemantics = 0x01000000,
            OpenReparsePoint = 0x00200000,
            OpenNoRecall = 0x00100000,
            FirstPipeInstance = 0x00080000

            // ReSharper restore UnusedMember.Local
        }

        private class Win32
        {
            // ReSharper disable InconsistentNaming
            public const int ERROR_SHARING_VIOLATION = 32;
            public const int ERROR_FILE_NOT_FOUND = 2;
            // ReSharper restore InconsistentNaming

            [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern SafeFileHandle CreateFile(
               string lpFileName,
               EFileAccess dwDesiredAccess,
               EFileShare dwShareMode,
               IntPtr lpSecurityAttributes,
               ECreationDisposition dwCreationDisposition,
               EFileAttributes dwFlagsAndAttributes,
               IntPtr hTemplateFile);

            [DllImport("kernel32.dll", EntryPoint = "DeleteFile", SetLastError = true, CharSet = CharSet.Unicode)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeleteFile(string lpFileName);
        }
        
        #endregion
    }
}
