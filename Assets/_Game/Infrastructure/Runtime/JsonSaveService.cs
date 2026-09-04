using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Game.Foundation;
using UnityEngine;

namespace Game.Infrastructure
{
    public sealed class JsonSaveService : ISaveService
    {
        private static readonly Regex ValidSlot = new Regex("^[A-Za-z0-9_-]{1,48}$", RegexOptions.Compiled);
        private readonly string saveDirectory;

        public JsonSaveService(string saveDirectory = null)
        {
            this.saveDirectory = saveDirectory ?? Path.Combine(Application.persistentDataPath, "Saves");
        }

        public bool Exists(string slot) => File.Exists(GetPath(slot));

        public async Task SaveAsync(string slot, int schemaVersion, string payloadJson, CancellationToken cancellationToken)
        {
            ValidateSlot(slot);
            if (schemaVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            }

            if (payloadJson == null)
            {
                throw new ArgumentNullException(nameof(payloadJson));
            }

            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(saveDirectory);

            var envelope = new SaveEnvelope
            {
                schemaVersion = schemaVersion,
                savedAtUtc = DateTime.UtcNow.ToString("O"),
                payloadJson = payloadJson,
                checksum = ComputeChecksum(schemaVersion, payloadJson)
            };

            var json = JsonUtility.ToJson(envelope, true);
            var target = GetPath(slot);
            var temporary = target + ".tmp";
            var backup = target + ".bak";

            try
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                    stream.Flush(true);
                }

                cancellationToken.ThrowIfCancellationRequested();
                Commit(temporary, target, backup);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        public async Task<SaveReadResult> LoadAsync(string slot, CancellationToken cancellationToken)
        {
            ValidateSlot(slot);
            var target = GetPath(slot);
            var primary = await TryReadAsync(target, false, cancellationToken);
            if (primary.Succeeded)
            {
                return primary;
            }

            var backup = await TryReadAsync(target + ".bak", true, cancellationToken);
            return backup.Succeeded ? backup : SaveReadResult.Failure($"Primary: {primary.Error}; Backup: {backup.Error}");
        }

        public void Delete(string slot)
        {
            var target = GetPath(slot);
            DeleteIfExists(target);
            DeleteIfExists(target + ".bak");
            DeleteIfExists(target + ".tmp");
        }

        private async Task<SaveReadResult> TryReadAsync(string path, bool recovered, CancellationToken cancellationToken)
        {
            if (!File.Exists(path))
            {
                return SaveReadResult.Failure("File does not exist.");
            }

            try
            {
                string json;
                using (var reader = new StreamReader(path, Encoding.UTF8))
                {
                    json = await reader.ReadToEndAsync();
                }

                cancellationToken.ThrowIfCancellationRequested();
                var envelope = JsonUtility.FromJson<SaveEnvelope>(json);
                if (envelope == null || envelope.schemaVersion < 1 || envelope.payloadJson == null)
                {
                    return SaveReadResult.Failure("Envelope is invalid.");
                }

                var checksum = ComputeChecksum(envelope.schemaVersion, envelope.payloadJson);
                if (!FixedTimeEquals(checksum, envelope.checksum))
                {
                    return SaveReadResult.Failure("Checksum mismatch.");
                }

                return SaveReadResult.Success(envelope.schemaVersion, envelope.payloadJson, recovered);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                return SaveReadResult.Failure(exception.Message);
            }
        }

        private string GetPath(string slot)
        {
            ValidateSlot(slot);
            return Path.Combine(saveDirectory, slot + ".json");
        }

        private static void ValidateSlot(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot) || !ValidSlot.IsMatch(slot))
            {
                throw new ArgumentException("Save slot may only contain letters, numbers, underscore and hyphen.", nameof(slot));
            }
        }

        private static void Commit(string temporary, string target, string backup)
        {
            if (!File.Exists(target))
            {
                File.Move(temporary, target);
                return;
            }

            try
            {
                File.Replace(temporary, target, backup, true);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(target, backup, true);
                File.Delete(target);
                File.Move(temporary, target);
            }
        }

        private static string ComputeChecksum(int schemaVersion, string payloadJson)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(schemaVersion + "|" + payloadJson);
                return Convert.ToBase64String(sha.ComputeHash(bytes));
            }
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            var a = Encoding.UTF8.GetBytes(left);
            var b = Encoding.UTF8.GetBytes(right);
            if (a.Length != b.Length)
            {
                return false;
            }

            var difference = 0;
            for (var index = 0; index < a.Length; index++)
            {
                difference |= a[index] ^ b[index];
            }

            return difference == 0;
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        [Serializable]
        private sealed class SaveEnvelope
        {
            public int schemaVersion;
            public string savedAtUtc;
            public string payloadJson;
            public string checksum;
        }
    }
}
