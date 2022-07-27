using Discord;
using Discord.Interactions;
using NodaTime;
using Shisho.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shisho.TypeConverters;

public class TimezoneAutoComplete : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var search = (autocompleteInteraction.Data.Current.Value as string ?? "US/")
            .ToUpperInvariant()
            .Split(' ');
        return Task.FromResult(AutocompletionResult.FromSuccess(timezoneProvider.Tzdb
            .Ids.Select(x => CalculateRank(search, x))
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .Select(x => new AutocompleteResult(x.Result, x.Result))
            .Take(5)
        ));
    }

    private static Rank CalculateRank(string[] search, string item)
    {
        int rank = 0;
        foreach (var x in search)
            if (item.ToUpperInvariant().Contains(x)) rank++;

        return new Rank(rank, item);
    }

    private record struct Rank (int Count, string Result);
    private readonly TimezoneProvider timezoneProvider;

    public TimezoneAutoComplete(TimezoneProvider timezoneProvider)
    {
        this.timezoneProvider = timezoneProvider;
    }
}
