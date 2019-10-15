﻿using System;
using System.Collections.Generic;
using System.IO;

using R5T.Pictia;


namespace R5T.Gepidia.Remote
{
    public static class RemoteFileSystem
    {
        public static bool Exists(SftpClientWrapper sftpClientWrapper, string path)
        {
            var output = sftpClientWrapper.SftpClient.Exists(path);
            return output;
        }

        /// <summary>
        /// Checks that the file path exists, and that it is a file.
        /// Note: requires two remote calls.
        /// </summary>
        public static bool ExistsFile(SftpClientWrapper sftpClientWrapper, string filePath)
        {
            // Does the path exist?
            var exists = RemoteFileSystem.Exists(sftpClientWrapper, filePath);
            if(!exists)
            {
                return false;
            }

            // The path exists, but is it a file?
            var isFile = RemoteFileSystem.IsFile(sftpClientWrapper, filePath);
            return isFile;
        }

        public static bool ExistsDirectory(SftpClientWrapper sftpClientWrapper, string directoryPath)
        {
            // Does the path exist?
            var exists = RemoteFileSystem.Exists(sftpClientWrapper, directoryPath);
            if (!exists)
            {
                return false;
            }

            // The path exists, but is it a file?
            var isFile = RemoteFileSystem.IsDirectory(sftpClientWrapper, directoryPath);
            return isFile;
        }

        public static bool IsFile(SftpClientWrapper sftpClientWrapper, string path)
        {
            var attributes = sftpClientWrapper.SftpClient.GetAttributes(path);

            var output = !attributes.IsDirectory;
            return output;
        }

        public static bool IsDirectory(SftpClientWrapper sftpClientWrapper, string path)
        {
            var attributes = sftpClientWrapper.SftpClient.GetAttributes(path);

            var output = attributes.IsDirectory;
            return output;
        }

        public static void CreateDirectory(SftpClientWrapper sftpClientWrapper, string directoryPath)
        {
            sftpClientWrapper.CreateDirectory(directoryPath); // Idempotent. No exception thrown.
        }

        public static void CreateDirectoryOnlyIfNotExists(SftpClientWrapper sftpClientWrapper, string directoryPath)
        {
            RemoteFileSystem.CreateDirectory(sftpClientWrapper, directoryPath);
        }

        public static void DeleteDirectory(SftpClientWrapper sftpClientWrapper, string directoryPath, bool recursive = true)
        {
            var exists = RemoteFileSystem.Exists(sftpClientWrapper, directoryPath);
            if (!exists)
            {
                return;
            }

            var isDirectory = RemoteFileSystem.IsDirectory(sftpClientWrapper, directoryPath);
            if(!isDirectory)
            {
                throw new Exception($"Unable to delete directory. Path was not a directory:\n{directoryPath}");
            }

            if(recursive)
            {
                sftpClientWrapper.DeleteDirectory(directoryPath);
            }
            else
            {
                sftpClientWrapper.SftpClient.DeleteDirectory(directoryPath);
            }
        }

        public static void DeleteDirectoryOnlyIfExists(SftpClientWrapper sftpClientWrapper, string directoryPath, bool recursive = true)
        {
            RemoteFileSystem.DeleteDirectory(sftpClientWrapper, directoryPath, recursive); // Idempotent, ok.
        }

        public static Stream CreateFile(SftpClientWrapper sftpClientWrapper, string filePath, bool overwrite = true)
        {
            RemoteFileSystem.CheckOverwrite(sftpClientWrapper, filePath, overwrite);

            var output = sftpClientWrapper.SftpClient.Create(filePath);
            return output;
        }

        public static Stream OpenFile(SftpClientWrapper sftpClientWrapper, string filePath)
        {
            var output = sftpClientWrapper.SftpClient.OpenWrite(filePath);
            return output;
        }

        public static Stream ReadFile(SftpClientWrapper sftpClientWrapper, string filePath)
        {
            var output = sftpClientWrapper.SftpClient.OpenRead(filePath);
            return output;
        }

        public static void DeleteFile(SftpClientWrapper sftpClientWrapper, string filePath)
        {
            sftpClientWrapper.SftpClient.DeleteFileOkIfNotExists(filePath);
        }

        public static void DeleteFileOnlyIfExists(SftpClientWrapper sftpClientWrapper, string filePath)
        {
            RemoteFileSystem.DeleteFile(sftpClientWrapper, filePath); // Idempotent, ok.
        }

        public static DateTime GetDirectoryLastModifiedTimeUTC(SftpClientWrapper sftpClientWrapper, string directoryPath)
        {
            var output = sftpClientWrapper.SftpClient.GetLastWriteTimeUtc(directoryPath);
            return output;
        }

        public static DateTime GetFileLastModifiedTimeUTC(SftpClientWrapper sftpClientWrapper, string filePath)
        {
            var output = sftpClientWrapper.SftpClient.GetLastWriteTimeUtc(filePath);
            return output;
        }

        public static void ChangePermissions(SftpClientWrapper sftpClientWrapper, string path, short mode)
        {
            sftpClientWrapper.SftpClient.ChangePermissions(path, mode);
        }

        public static void Copy(SftpClientWrapper sftpClientWrapper, Stream source, string destinationFilePath, bool overwrite = true)
        {
            RemoteFileSystem.CheckOverwrite(sftpClientWrapper, destinationFilePath, overwrite);

            using (var destination = sftpClientWrapper.SftpClient.Create(destinationFilePath))
            {
                source.CopyTo(destination);
            }
        }

        public static void Copy(SftpClientWrapper sftpClientWrapper, string sourceFilePath, Stream destination)
        {
            using (var source = sftpClientWrapper.SftpClient.OpenRead(sourceFilePath))
            {
                source.CopyTo(destination);
            }
        }

        public static void CopyFile(SftpClientWrapper sftpClientWrapper, string sourceFilePath, string destinationFilePath, bool overwrite = true)
        {
            var fileExists = RemoteFileSystem.ExistsFile(sftpClientWrapper, sourceFilePath);
            if(!fileExists)
            {
                throw new Exception($"Unable to copy file. Source file does not exist:\n{sourceFilePath}");
            }

            using (var sshClient = sftpClientWrapper.SftpClient.ConnectionInfo.GetSshClient())
            {
                var commandText = $"cp \"{sourceFilePath}\" \"{destinationFilePath}\"";
                using (var command = sshClient.RunCommand(commandText))
                {
                    if (command.ExitStatus != 0)
                    {
                        throw new Exception($"Command failed. Result:\n{command.Result}");
                    }
                }
            }
        }

        public static void CopyDirectory(SftpClientWrapper sftpClientWrapper, string sourceDirectoryPath, string destinationDirectoryPath)
        {
            var directoryExists = RemoteFileSystem.ExistsDirectory(sftpClientWrapper, sourceDirectoryPath);
            if (!directoryExists)
            {
                throw new Exception($"Unable to copy directory. Source directory does not exist:\n{sourceDirectoryPath}");
            }

            using (var sshClient = sftpClientWrapper.SftpClient.ConnectionInfo.GetSshClient())
            {
                var commandText = $"cp -r \"{sourceDirectoryPath}\" \"{destinationDirectoryPath}\"";
                using (var command = sshClient.RunCommand(commandText))
                {
                    if (command.ExitStatus != 0)
                    {
                        throw new Exception($"Command failed. Result:\n{command.Result}");
                    }
                }
            }
        }

        public static void MoveDirectory(SftpClientWrapper sftpClientWrapper, string sourceDirectoryPath, string destinationDirectoryPath)
        {
            var directoryExists = RemoteFileSystem.ExistsDirectory(sftpClientWrapper, sourceDirectoryPath);
            if (!directoryExists)
            {
                throw new Exception($"Unable to move directory. Source directory does not exist:\n{sourceDirectoryPath}");
            }

            using (var sshClient = sftpClientWrapper.SftpClient.ConnectionInfo.GetSshClient())
            {
                var commandText = $"mv \"{sourceDirectoryPath}\" \"{destinationDirectoryPath}\"";
                using (var command = sshClient.RunCommand(commandText))
                {
                    if (command.ExitStatus != 0)
                    {
                        throw new Exception($"Command failed. Result:\n{command.Result}");
                    }
                }
            }
        }

        public static void MoveFile(SftpClientWrapper sftpClientWrapper, string sourceFilePath, string destinationFilePath, bool overwrite = true)
        {
            var fileExists = RemoteFileSystem.ExistsFile(sftpClientWrapper, sourceFilePath);
            if (!fileExists)
            {
                throw new Exception($"Unable to move file. Source file does not exist:\n{sourceFilePath}");
            }

            RemoteFileSystem.CheckOverwrite(sftpClientWrapper, destinationFilePath, overwrite);

            using (var sshClient = sftpClientWrapper.SftpClient.ConnectionInfo.GetSshClient())
            {
                var commandText = $"mv \"{sourceFilePath}\" \"{destinationFilePath}\"";
                using (var command = sshClient.RunCommand(commandText))
                {
                    if (command.ExitStatus != 0)
                    {
                        throw new Exception($"Command failed. Result:\n{command.Result}");
                    }
                }
            }
        }

        public static IEnumerable<string> EnumerateDirectories(SftpClientWrapper sftpClientWrapper, string directoryPath)
        {
            var sftpFiles = sftpClientWrapper.SftpClient.ListDirectory(directoryPath);
            foreach (var sftpFile in sftpFiles)
            {
                if(sftpFile.IsDirectory)
                {
                    yield return sftpFile.FullName;
                }
            }
        }

        public static IEnumerable<string> EnumerateFiles(SftpClientWrapper sftpClientWrapper, string directoryPath)
        {
            var sftpFiles = sftpClientWrapper.SftpClient.ListDirectory(directoryPath);
            foreach (var sftpFile in sftpFiles)
            {
                if (!sftpFile.IsDirectory)
                {
                    yield return sftpFile.FullName;
                }
            }
        }

        public static IEnumerable<string> EnumerateFileSystemEntries(SftpClientWrapper sftpClientWrapper, string directoryPath, bool recursive = false)
        {
            var sftpFiles = sftpClientWrapper.SftpClient.ListDirectory(directoryPath);
            foreach (var sftpFile in sftpFiles)
            {
                yield return sftpFile.FullName;

                if(recursive && sftpFile.IsDirectory)
                {
                    var subDirectoryFileSystemEntries = RemoteFileSystem.EnumerateFileSystemEntries(sftpClientWrapper, sftpFile.FullName, true);
                    foreach (var subDirectoryFileSystemEntry in subDirectoryFileSystemEntries)
                    {
                        yield return subDirectoryFileSystemEntry;
                    }
                }
            }
        }


        #region Miscellaneous

        public static void CheckOverwrite(SftpClientWrapper sftpClientWrapper, string filePath, bool overwrite)
        {
            if (!overwrite && RemoteFileSystem.ExistsFile(sftpClientWrapper, filePath))
            {
                var exception = CommonFileSystem.GetCannotOverwriteFileIOException(filePath);
                throw exception;
            }
        }

        #endregion
    }
}