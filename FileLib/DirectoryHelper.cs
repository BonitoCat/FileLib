using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FileLib
{
    public class DirectoryHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Passwd
        {
            public IntPtr pw_name;
            public IntPtr pw_passwd;
            public uint pw_uid;
            public uint pw_gid;
            public IntPtr pw_gecos;
            public IntPtr pw_dir;
            public IntPtr pw_shell;
        }

        [DllImport("libc")]
        private static extern IntPtr getpwuid(uint uid);
        
        [DllImport("libc", SetLastError = true)]
        private static extern int chmod(string pathname, uint mode);
        
        [DllImport("libc", SetLastError = true)]
        private static extern int chown(string path, uint owner, uint group);
        
        /// <param name="excludeSudo">If true, will return the users home folder instead of the root one on Unix</param>
        /// <returns>The OS specific home folder</returns>
        public static string GetUserDirPath(bool excludeSudo = true)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return home;
            }

            if (Environment.UserName != "root" || !excludeSudo)
            {
                return home;
            }

            string sudoUidStr = Environment.GetEnvironmentVariable("SUDO_UID");
            if (!uint.TryParse(sudoUidStr, out uint sudoUid))
            {
                return home;
            }

            IntPtr pwdUid = getpwuid(sudoUid);
            if (pwdUid == IntPtr.Zero)
            {
                return home;
            }

            Passwd pwd = Marshal.PtrToStructure<Passwd>(pwdUid);
            return Marshal.PtrToStringAnsi(pwd.pw_dir) ?? home;
        }

        /// <param name="excludeSudo">If true, will return the users app data folder instead of the root one on Unix</param>
        /// <returns>The OS specific app data folder</returns>
        /// <exception cref="PlatformNotSupportedException">Thrown when not on Windows or Unix</exception>
        public static string GetAppDataDirPath(bool excludeSudo = true)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                if (!string.IsNullOrEmpty(xdg))
                {
                    return xdg;
                }

                return Path.Combine(GetUserDirPath(excludeSudo), ".local", "share");
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Path.Combine(GetUserDirPath(excludeSudo), "Library", "Application Support");
            }

            throw new PlatformNotSupportedException();
        }
        
        /// <param name="excludeSudo">If true, will return the users app config folder instead of the root one on Unix</param>
        /// <returns>The OS specific app config folder</returns>
        /// <exception cref="PlatformNotSupportedException">Thrown when not on Windows or Unix</exception>
        public static string GetAppConfigDirPath(bool excludeSudo = true)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (!string.IsNullOrEmpty(xdg))
                {
                    return xdg;
                }

                return Path.Combine(GetUserDirPath(excludeSudo), ".config");
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Path.Combine(GetUserDirPath(excludeSudo), "Library", "Application Support");
            }

            throw new PlatformNotSupportedException();
        }

        public static string GetCacheDirPath(bool excludeSudo = true)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cache");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
                if (!string.IsNullOrEmpty(xdg))
                {
                    return xdg;
                }

                return Path.Combine(GetUserDirPath(excludeSudo), ".cache");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Path.Combine(GetUserDirPath(excludeSudo), "Library", "Caches");
            }

            throw new PlatformNotSupportedException();
        }

        /// <param name="path">The path of the folder to be created</param>
        /// <param name="userOwned">If true, the folder will be owned by the user, even if run with root on Unix</param>
        /// <returns>True if the folder was created, false if it already exists</returns>
        public static void CreateDir(string path, bool userOwned = true)
        {
            try
            {
                Directory.CreateDirectory(path);
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
        
        /// <summary>
        /// Copies the <paramref name="sourceDir"/> and its content to <paramref name="destinationDir"/>
        /// </summary>
        /// <param name="sourceDir">The folder to copy</param>
        /// <param name="destinationDir">The location of the copy</param>
        /// <param name="recursive">If true, will copy all subdirectories as well</param>
        /// <exception cref="DirectoryNotFoundException">Thrown if the <paramref name="sourceDir"/> couldn't be found</exception>
        public void CopyDir(string sourceDir, string destinationDir, bool recursive = true)
        {
            DirectoryInfo dir = new(sourceDir);
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDir(subDir.FullName, newDestinationDir, true);
                }
            }
        }
    }
}