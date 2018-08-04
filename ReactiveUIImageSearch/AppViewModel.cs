using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ReactiveUI;
using ReactiveUI.Xaml;
using System.Windows;
using System.Reactive.Linq;
using System.Xml.Linq;
using System.Globalization;
using System.Web;
using System.Text.RegularExpressions;

namespace ReactiveUIImageSearch
{
    public class AppViewModel : ReactiveObject
    {
        String _SearchTerm;
        public String SearchTerm
        {
            get { return _SearchTerm; }
            set { this.RaiseAndSetIfChanged(x => x.SearchTerm, value); }
        }
        public ReactiveAsyncCommand ExecuteSearch { get; protected set; }

        ObservableAsPropertyHelper<List<FlickrPhoto>> _SearchResults;
        public List<FlickrPhoto> SearchResults
        {
            get { return this._SearchResults.Value; }
        }

        ObservableAsPropertyHelper<Visibility> _SpinnerVisibility;
        public Visibility SpinnerVisibility { get { return _SpinnerVisibility.Value; } }


        public AppViewModel(ReactiveAsyncCommand testExecuteSearchCommand = null,
    IObservable<List<FlickrPhoto>> testSearchResults = null)
        {
            ExecuteSearch = testExecuteSearchCommand ?? new ReactiveAsyncCommand();

            this.ObservableForProperty(x => x.SearchTerm)
                    .Throttle(TimeSpan.FromMilliseconds(800), RxApp.DeferredScheduler)
                    .Select(x => x.Value)
                    .DistinctUntilChanged()
                    .Where(x => !String.IsNullOrWhiteSpace(x))
                    .InvokeCommand(ExecuteSearch);

            _SpinnerVisibility = ExecuteSearch.ItemsInflight
                .Select(x => x > 0 ? Visibility.Visible : Visibility.Collapsed)
                .ToProperty(this, x => x.SpinnerVisibility, Visibility.Hidden);

            IObservable<List<FlickrPhoto>> results;
            if (testSearchResults != null)
            {
                results = testSearchResults;
            }
            else
            {
                results = ExecuteSearch.RegisterAsyncFunction(term => GetSearchResultsFromFlickr((String)term));
            }
            _SearchResults = results.ToProperty(this, x => x.SearchResults, new List<FlickrPhoto>());
        }

        private static List<FlickrPhoto> GetSearchResultsFromFlickr(string searchTerm)
        {
            var doc = XDocument.Load(String.Format(CultureInfo.InvariantCulture,
                "http://api.flickr.com/services/feeds/photos_public.gne?tags={0}&format=rss_200",
                HttpUtility.UrlEncode(searchTerm)));
            if (doc.Root == null)
                return null;
            var titles = doc.Root.Descendants("{http://search.yahoo.com/mrss/}title").Select(x => x.Value);
            var tagRegex = new Regex("<[^>]+>", RegexOptions.IgnoreCase);
            var descriptions = doc.Root.Descendants("{http://search.yahoo.com/mrss/}description")
                                .Select(x => tagRegex.Replace(HttpUtility.HtmlDecode(x.Value), ""));
            var items = titles.Zip(descriptions,
                                    (t, d) => new FlickrPhoto { Title = t, Description = d }).ToArray();
            var urls = doc.Root.Descendants("{http://search.yahoo.com/mrss/}thumbnail")
                                .Select(x => x.Attributes("url").First().Value);
            var ret = items.Zip(urls, (item, url) =>
            {
                item.Url = url; return item;
            }).ToList();
            return ret;
        }
    }
}
