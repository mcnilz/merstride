using System.Globalization;
using Merstride.Application;
using Merstride.Domain;

namespace Merstride.Infrastructure;

public sealed class IdentityAllocator(RunStore store) : IRequestIds
{
    public string Request() => Allocate('q');
    public string Run() => Allocate('r');

    private string Allocate(char kind)
    {
        var directory = Path.Combine(store.Root, ".identities");
        Directory.CreateDirectory(directory);
        using var held = RunStore.Exclusive(Path.Combine(directory, "allocator.lock"), "IDENTITY_LOCKED", "Another caller is reserving an identity. Retry the command explicitly.");
        var sequence = Path.Combine(directory, "sequence");
        ulong last = 0;
        if (File.Exists(sequence))
        {
            if (!ulong.TryParse(File.ReadAllText(sequence), NumberStyles.None, CultureInfo.InvariantCulture, out last))
                throw new MerstrideException("CORRUPT_IDENTITIES", "Identity reservations are corrupt; no identity was reused. Preserve the state root and inspect its storage.", 6);
        }
        // Reservations are permanent, including identities consumed by failed writes.
        while (last < ulong.MaxValue)
        {
            var id = kind + "_" + (++last).ToString("x6", CultureInfo.InvariantCulture);
            if (Directory.Exists(Path.Combine(store.Root, id)) || Directory.Exists(Path.Combine(store.Root, ".trash", id))) continue;
            var reservation = Path.Combine(directory, id);
            try
            {
                using var file = new FileStream(reservation, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                file.Flush(true);
            }
            catch (IOException) when (File.Exists(reservation)) { continue; }
            RunStore.AtomicWrite(sequence, stream =>
            {
                var bytes = System.Text.Encoding.ASCII.GetBytes(last.ToString(CultureInfo.InvariantCulture));
                stream.Write(bytes);
            });
            return id;
        }
        throw new MerstrideException("IDENTITY_EXHAUSTED", "This state root has exhausted its identity space; no identity was reused.", 6);
    }
}
