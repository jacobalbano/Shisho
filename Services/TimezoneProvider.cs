using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.TimeZones;
using Shisho.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Shisho.Services;

[AutoDiscoverSingletonService, ForceInitialization]
public class TimezoneProvider
{
    public TimezoneProvider(Orchestrator orchestrator, ILogger<TimezoneProvider> logger)
    {
        Tzdb = DateTimeZoneProviders.Tzdb;
        actor = new InfrequentActor(Duration.FromHours(12));
        orchestrator.OnTick += (_, _) => actor.Act(CheckForTimezones);

        this.logger = logger;

        EnsureDirectorySanity();
        LoadFileIfExists();
    }

    private async Task CheckForTimezones()
    {
        logger.LogInformation("Checking for tzdb updates");

        using (var client = new HttpClient())
        {
            try
            {
                EnsureDirectorySanity();

                var latest = (await client.GetStringAsync("https://nodatime.org/tzdb/latest.txt"))
                    .Trim();

                if (File.Exists(latestFilepath))
                {
                    var lastLatest = (await File.ReadAllTextAsync(latestFilepath))
                        .Trim();

                    if (lastLatest == latest)
                    {
                        logger.LogInformation("No tzdb update found");
                        return;
                    }
                }

                //  download the database to a .temp file
                using (var stream = await client.GetStreamAsync(latest))
                using (var filestream = File.OpenWrite(dbFilepathTemp))
                    await stream.CopyToAsync(filestream);

                //  rename the old file to .pending
                if (File.Exists(dbFilepath))
                    File.Move(dbFilepath, dbFilepathPending);

                //  rename the new file to have the standard name
                File.Move(dbFilepathTemp, dbFilepath);
                
                //  delete the pending file if it exists
                if (File.Exists(dbFilepathPending))
                    File.Delete(dbFilepathPending);

                //  save the url to the newly downloaded file and load it into the system
                await File.WriteAllTextAsync(latestFilepath, latest);
                await Task.Run(() => LoadFileIfExists());

                logger.LogInformation("Downloaded new tzdb");

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error checking for timezone update");
            }
        }
    }

    private void EnsureDirectorySanity()
    {
        /*
         * ideal state when beginning an operation:
         * - tzdb.nzd exists
         * - tzdb.nzd.pending does NOT exist (it should have been deleted)
         * - tzdb.nzd.temp does NOT exist (it should have replaced the main file)
         */

        if (!Directory.Exists(tzdbDir))
        {
            Directory.CreateDirectory(tzdbDir);
            return; // nothing else to do; obviously no files will exist
        }

        if (!File.Exists(dbFilepath))
        {
            //  must have failed right after renaming to .pending
            if (!File.Exists(dbFilepathPending))
                return; // shouldn't be possible, but counts as blank slate

            //  move the file back
            File.Move(dbFilepathPending, dbFilepath);
        }

        //  shouldn't be possible for this to exist at this point
        if (File.Exists(dbFilepathPending))
            File.Delete(dbFilepathPending);

        //  we'll download a new one if necessary
        if (File.Exists(dbFilepathTemp))
            File.Delete(dbFilepathTemp);
    }

    private void LoadFileIfExists()
    {
        try
        {
            if (File.Exists(dbFilepath))
            {
                using var stream = File.OpenRead(dbFilepath);
                var source = TzdbDateTimeZoneSource.FromStream(stream);
                Tzdb = new DateTimeZoneCache(source);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error loading tzdb");
        }
    }

    private static readonly string tzdbDir = "tzdb";
    private static readonly string latestFilepath = Path.Combine(tzdbDir, "latest.txt");
    private static readonly string dbFilepath = Path.Combine(tzdbDir, "tzdb.nzd");
    private static readonly string dbFilepathTemp = $"{dbFilepath}.temp";
    private static readonly string dbFilepathPending = $"{dbFilepath}.pending";

    private readonly InfrequentActor actor;
    private readonly ILogger<TimezoneProvider> logger;

    public IDateTimeZoneProvider Tzdb { get; private set; }
}
