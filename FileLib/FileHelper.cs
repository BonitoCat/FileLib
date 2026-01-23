using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FileLib
{
    public static class FileHelper
    {
        [DllImport("libc", SetLastError = true)]
        private static extern int chmod(string pathname, uint mode);
        
        [DllImport("libc", SetLastError = true)]
        private static extern int chown(string path, uint owner, uint group);
        
        /// <param name="path">The path of the file to be created</param>
        /// <param name="userOwned">If true, the file will be owned by the user, even if run with root on Unix</param>
        public static void Create(string path, bool userOwned = true)
        {
            try
            {
                File.Create(path).Close();
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && userOwned)
                {
                    chmod(path, (uint) (UnixFileModes.UserRWX | UnixFileModes.GroupRX | UnixFileModes.OtherRX));
                
                    string sudoUidStr = Environment.GetEnvironmentVariable("SUDO_UID");
                    string sudoGidStr = Environment.GetEnvironmentVariable("SUDO_GID");

                    if (sudoUidStr != null && sudoGidStr != null &&
                        uint.TryParse(sudoUidStr, out uint uid) &&
                        uint.TryParse(sudoGidStr, out uint gid))
                    {
                        chown(path, uid, gid);
                    }
                }
            }
            catch (IOException) { }
        }
    }
}