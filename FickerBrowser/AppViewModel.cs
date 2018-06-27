// <copyright file="MainWindow.xaml.cs" company="Lightstaff">
// Copyright (c) Lightstaff. All rights reserved.
// </copyright>

namespace FickerBrowser
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Reactive.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Xml.Linq;
    using ReactiveUI;

    public class AppViewModel : ReactiveObject
    {
        // ↓ こいつらはクラスメンバとして隠匿される

        private string searchTerm;

        private ObservableAsPropertyHelper<List<FickerPhoto>> searchResults;

        private ObservableAsPropertyHelper<Visibility> spinnerVisibility;

        // ↑ こいつらはクラスメンバとして隠匿される

        // 基本的にコンストラクタでイベントやプロパティの変更処理を定義するのが慣わし・・・なのか？
        public AppViewModel()
        {
            // コマンドに実装を結びつける
            this.ExecuteSearch = ReactiveCommand.CreateFromTask<string, List<FickerPhoto>>(searchTerm => GetSearchResultsFromFicker(searchTerm));

            // SearchTermの観測
            // Throttleは入力完了を待つための遅延だよね？
            // DistinctUntilChangedは”Returns an observable sequence that contains only distinct contiguous elements.”とのこと
            // 最終的にExecuteSearchを呼ぶ
            this.WhenAnyValue(x => x.SearchTerm)
                .Throttle(TimeSpan.FromMilliseconds(800), RxApp.MainThreadScheduler)
                .Select(x => x?.Trim())
                .DistinctUntilChanged()
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .InvokeCommand(this.ExecuteSearch);

            // スピナーはExecuteSearchが実行されている間（IsExecuting）、プロパティを変化させる
            // ExecuteSearchを観測しているとも言える
            this.spinnerVisibility = this.ExecuteSearch.IsExecuting
                .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                .ToProperty(this, x => x.SpinnerVisibility, Visibility.Hidden);

            // 検索結果にExecuteSearchの結果（List<FickerPhoto>）を渡す
            // こちらもまたExecuteSearchを観測しているとも言える
            this.searchResults = this.ExecuteSearch.ToProperty(this, x => x.SearchResults, new List<FickerPhoto>());

            // ExecuteSearchのエラーはこうやってハンドルするらしい
            // 解放処理（Unsbscribe）はいらんのだろうか？
            this.ExecuteSearch.ThrownExceptions.Subscribe(ex => {/* Handle errors here */});
        }

        /// <summary>
        /// 検索フレーズ
        /// </summary>
        public string SearchTerm
        {
            get
            {
                return this.searchTerm;
            }

            set
            {
                // 変更の通知が起きる = Reactive
                this.RaiseAndSetIfChanged(ref this.searchTerm, value);
            }
        }

        /// <summary>
        /// 検索結果
        /// </summary>
        public List<FickerPhoto> SearchResults => this.searchResults.Value;

        /// <summary>
        /// スピナー（"..."というTextBlock）の表示プロパティ
        /// </summary>
        public Visibility SpinnerVisibility => this.spinnerVisibility.Value;

        /// <summary>
        /// 検索実行コマンド
        /// </summary>
        public ReactiveCommand<string, List<FickerPhoto>> ExecuteSearch { get; protected set; }

        /// <summary>
        /// Ficker検索（非同期）
        /// </summary>
        /// <param name="searchTerm">検索フレーズ</param>
        /// <returns>検索結果</returns>
        private static async Task<List<FickerPhoto>> GetSearchResultsFromFicker(string searchTerm)
        {
            var doc = await Task.Run(() => XDocument.Load(string.Format(
                CultureInfo.InvariantCulture,
                "http://api.flickr.com/services/feeds/photos_public.gne?tags={0}&format=rss_200",
                WebUtility.UrlEncode(searchTerm))));

            if (doc.Root == null)
            {
                return null;
            }

            var titles = doc.Root.Descendants("{http://search.yahoo.com/mrss/}title")
                .Select(x => x.Value);

            var tagRegex = new Regex("<[^>]+>", RegexOptions.IgnoreCase);
            var descriptions = doc.Root.Descendants("{http://search.yahoo.com/mrss/}description")
                .Select(x => tagRegex.Replace(WebUtility.HtmlDecode(x.Value), string.Empty));

            var items = titles.Zip(descriptions, (t, d) => new FickerPhoto()
            {
                Title = t,
                Description = d,
            }).ToArray();

            var urls = doc.Root.Descendants("{http://search.yahoo.com/mrss/}thumbnail")
                .Select(x => x.Attributes("url").First().Value);

            var ret = items.Zip(urls, (item, url) =>
            {
                item.Url = url;
                return item;
            }).ToList();

            return ret;
        }
    }
}
