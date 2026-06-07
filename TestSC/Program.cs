using System;
using System.Linq;
using System.Threading.Tasks;
using SoundCloudExplode;

class Program {
    static async Task Main() {
        var sc = new SoundCloudClient();
        try {
            var search = await sc.Search.GetTracksAsync("Ðáy Bi?n").ToListAsync();
            var track = search.FirstOrDefault();
            if (track != null) {
                var streamUrl = await sc.Tracks.GetDownloadUrlAsync(track);
                Console.WriteLine("SUCCESS! URL: " + streamUrl);
            } else {
                Console.WriteLine("NO RESULTS");
            }
        } catch (Exception ex) {
            Console.WriteLine("ERROR: " + ex.Message);
        }
    }
}
