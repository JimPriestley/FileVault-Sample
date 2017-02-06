using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.KeyVault;
using System.Threading;
using System.IO;
using System.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Net;

namespace FileVault
{

    public class FileVault
    {
        CloudStorageAccount storageAccount;

        //Empty Constructor expects storage account info in Appconfig file as "accountname" and "accountkey"
        public FileVault()
        {
            ServicePointManager.DefaultConnectionLimit = Environment.ProcessorCount * 8;
            ServicePointManager.Expect100Continue = false;

            StorageCredentials creds = new StorageCredentials(ConfigurationManager.AppSettings["accountName"], ConfigurationManager.AppSettings["accountKey"]);
            storageAccount = new CloudStorageAccount(creds, useHttps: true);
        }

        //This constructor allows you to pass in the storage account name and key as parameters.
        public FileVault(string StorageAccountName, string StorageKey)
        {
            ServicePointManager.DefaultConnectionLimit = Environment.ProcessorCount * 8;
            ServicePointManager.Expect100Continue = false;

            StorageCredentials creds = new StorageCredentials(StorageAccountName, StorageKey);
            storageAccount = new CloudStorageAccount(creds, useHttps: true);
        }

        //Create Container if it does not exist
        public void CreateContainer(string ContainerName)
        {
            // Create the blob client.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve a reference to a container.
            CloudBlobContainer container = blobClient.GetContainerReference(ContainerName);

            // Create the container if it doesn't already exist.
            // By default, the new container is private, meaning that you must specify your storage access key to download blobs from this container.
            container.CreateIfNotExists();
        }

        //Delete Container if it exists
        public void DeleteContainer(string ContainerName)
        {
            // Create the blob client.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve a reference to a container.
            CloudBlobContainer container = blobClient.GetContainerReference(ConfigurationManager.AppSettings["container"]);

            // Delete the container if it exists.
            container.DeleteIfExists();
        }

        //Get Container refference
        public CloudBlobContainer GetContainerRefference(string ContainerName)
        {
            // Create the blob client.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve a reference to a container.
            return blobClient.GetContainerReference(ContainerName);
        }

        //call AAD to get OAuth token using "clientId" and "clientSecret" from app settings
        private async static Task<string> GetToken(string authority, string resource, string scope)
        {
            var authContext = new AuthenticationContext(authority);
            ClientCredential clientCred = new ClientCredential(
                ConfigurationManager.AppSettings["clientId"],
                ConfigurationManager.AppSettings["clientSecret"]);
            AuthenticationResult result = await authContext.AcquireTokenAsync(resource, clientCred);

            if (result == null)
                throw new InvalidOperationException("Failed to obtain the JWT token");

            return result.AccessToken;
        }

        //Gets a resolver object, used to resolve a keyvault key
        public KeyVaultKeyResolver GetResolver()
        {
            // The Resolver object is used to interact with Key Vault for Azure Storage.
            return new KeyVaultKeyResolver(GetToken);
        }

        //uses a resolver object to retrieve a keyvault key
        public Microsoft.Azure.KeyVault.Core.IKey GetKey(KeyVaultKeyResolver Resolver)
        {
            return Resolver.ResolveKeyAsync("https://fvdemokeyvault.vault.azure.net/keys/FileVaultKey", CancellationToken.None).GetAwaiter().GetResult();
        }

        //Write Blob to Storage from Stream, using Encryption to the named container
        public void WriteBlobStream(Stream FileStream, string FileName, string ContainerName, Microsoft.Azure.KeyVault.Core.IKey Key)
        {
            CloudBlobContainer container = GetContainerRefference(ContainerName);

            // Now you simply use the RSA key to encrypt by setting it in the BlobEncryptionPolicy.
            BlobEncryptionPolicy policy = new BlobEncryptionPolicy(Key, null);
            BlobRequestOptions options = new BlobRequestOptions() { EncryptionPolicy = policy };

            // Retrieve reference to a blob named "myblob".
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(FileName.ToString());

            // Create or overwrite the "FileName" blob with contents from FileStream, using encryption option
            blockBlob.UploadFromStream(FileStream, FileStream.Length, null, options, null);
        }

        //Ubload a blob from a stream to the named container
        public void WriteBlobStream(Stream FileStream, string FileName, string ContainerName)
        {
            CloudBlobContainer container = GetContainerRefference(ContainerName);

            // Retrieve reference to a blob named "myblob".
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(FileName.ToString());

            // Create or overwrite the "FileName" blob with contents from FileStream
            blockBlob.UploadFromStream(FileStream, FileStream.Length);
        }

        //Upload a blob from a stream to a named container, using a KeyVault Key to dynamically encryprt the file
        public void WriteBlobFile(string FileName, string BlobName, string ContainerName, Microsoft.Azure.KeyVault.Core.IKey Key)
        {
            CloudBlobContainer container = GetContainerRefference(ContainerName);

            // Now you simply use the RSA key to encrypt by setting it in the BlobEncryptionPolicy.
            BlobEncryptionPolicy policy = new BlobEncryptionPolicy(Key, null);
            BlobRequestOptions options = new BlobRequestOptions() { EncryptionPolicy = policy };

            // Retrieve reference to a blob named "myblob".
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(BlobName);

            // Create or overwrite the "FileName" blob with contents from FileStream, using encryption option
            blockBlob.UploadFromFile(FileName, null, options, null);
        }

        //Upload a file as a blob to a named container
        public void WriteBlobFile(string FileName, string BlobName, string ContainerName)
        {
            CloudBlobContainer container = GetContainerRefference(ContainerName);

            // Retrieve reference to a blob named "myblob".
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(BlobName.ToString());

            // Create or overwrite the "FileName" blob with contents from FileStream
            blockBlob.UploadFromFile(FileName);
        }

        //Copies a directory of files to Blob, with encryption. The full origin path is preserved. 
        public void WriteDirectory(string DirectoryPath, bool Recurse, string ContainerName, Microsoft.Azure.KeyVault.Core.IKey Key)
        {
            string[] files;
            CreateContainer(ContainerName);

            if (Recurse)
            {
                files = Directory.GetFiles(DirectoryPath, "*.*", SearchOption.AllDirectories);
            }
            else
            {
                files = Directory.GetFiles(DirectoryPath, "*.*", SearchOption.TopDirectoryOnly);
            }

            Parallel.ForEach(files, (currentFile) =>
            {
                WriteBlobFile(currentFile, GetFileNameWithoutDriveLetter(currentFile), ContainerName, Key);
            });
        }

        //Copies a directory of files to Blob. The full origin path is preserved. 
        public void WriteDirectory(string DirectoryPath, bool Recurse, string ContainerName)
        {
            string[] files;

            CreateContainer(ContainerName);

            if (Recurse)
            {
                files = Directory.GetFiles(DirectoryPath, "*.*", SearchOption.AllDirectories);
            }
            else
            {
                files = Directory.GetFiles(DirectoryPath, "*.*", SearchOption.TopDirectoryOnly);
            }

            Parallel.ForEach(files, (currentFile) =>
            {
                WriteBlobFile(currentFile, GetFileNameWithoutDriveLetter(currentFile), ContainerName);
                Console.WriteLine(currentFile);
            });
        }

        //Strips the Drive and Colon off the front of the filename and also replaces '\' with '/'
        public string GetFileNameWithoutDriveLetter(string FileName)
        {
            FileName =  FileName.Substring(2).Replace('\\', '/');

            if (FileName.Substring(0, 1) == "/")
            {
                return FileName.Substring(1);
            }
            else
            {
                return FileName;
            }

        }

        public string ConvertFileNameToBackSlash(string FileName)
        {
            return FileName.Replace('/', '\\');
        }

        //Read Blob to Stream from a Container
        public Stream GetBlobStream(string BlobName, string ContainerName)
        {
            CloudBlobContainer container = GetContainerRefference(ContainerName);

            // Retrieve reference to a blob named "myblob".
            CloudBlockBlob blob = container.GetBlockBlobReference(BlobName);

            return blob.OpenRead();
        }

        //Read Encrypted blob to a Stream from a container
        public Stream GetBlobStream(string BlobName, string ContainerName, KeyVaultKeyResolver KeyResolver)
        {
            CloudBlobContainer container = GetContainerRefference(ContainerName);

            // In this case, we will not pass a key and only pass the resolver because
            // this policy will only be used for downloading / decrypting.
            BlobEncryptionPolicy policy = new BlobEncryptionPolicy(null, KeyResolver);
            BlobRequestOptions options = new BlobRequestOptions() { EncryptionPolicy = policy };

            // Retrieve reference to a blob named "myblob".
            CloudBlockBlob blob = container.GetBlockBlobReference(BlobName);

            return blob.OpenRead(null, options, null);
        }


        //Read Blob to a File at the path location specified from a Container
        public void GetBlobFile(string BlobName, string ContainerName, string DestinationPath)
        {
            CloudBlobContainer container = GetContainerRefference(ContainerName);

            // Retrieve reference to a blob named "myblob".
            CloudBlockBlob blob = container.GetBlockBlobReference(BlobName);

            blob.DownloadToFile(DestinationPath, FileMode.Create);
        }

        //Read Encrypted blob to a File at the path location specified from a container
        public void GetBlobFile(string BlobName, string ContainerName, string DestinationPath, KeyVaultKeyResolver KeyResolver)
        {
            CloudBlobContainer container = GetContainerRefference(ContainerName);

            // In this case, we will not pass a key and only pass the resolver because
            // this policy will only be used for downloading / decrypting.
            BlobEncryptionPolicy policy = new BlobEncryptionPolicy(null, KeyResolver);
            BlobRequestOptions options = new BlobRequestOptions() { EncryptionPolicy = policy };

            // Retrieve reference to a blob named "myblob".
            CloudBlockBlob blob = container.GetBlockBlobReference(BlobName);

            blob.DownloadToFile(DestinationPath, FileMode.Create, null, options, null);
        }

        //Read the files from a container that match the prefix pattern to a specified path, preserving orriginal tree structure.
        public void GetBlobs(string DestinationPath, string ContainerName, string Prefix)
        {
            CloudBlobContainer container = GetContainerRefference(ContainerName);

            var files = container.ListBlobs(Prefix, true);

            Parallel.ForEach(files, (currentFile) =>
            {
                GetBlobFile(currentFile.ToString(), ContainerName, DestinationPath + currentFile.ToString());
            });
        }

        //Read the encrypted files from a container that match the wildcard pattern to a specified path, preserving orriginal tree structure.
        public void GetBlobs(string DestinationPath, string ContainerName, string Prefix, KeyVaultKeyResolver KeyResolver)
        {
            CloudBlobContainer container = GetContainerRefference(ContainerName);

            var files = container.ListBlobs(Prefix, true);

            Parallel.ForEach(files, (currentFile) =>
            {
                GetBlobFile(currentFile.ToString(), ContainerName, DestinationPath + currentFile.ToString(), KeyResolver);
            });
        }

    }
}
