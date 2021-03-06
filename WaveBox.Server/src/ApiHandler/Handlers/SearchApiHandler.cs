using System;
using System.Collections.Generic;
using System.Web;
using Newtonsoft.Json;
using Ninject;
using WaveBox.Core.Injected;
using WaveBox.Model;
using WaveBox.Service.Services.Http;
using WaveBox.Static;

namespace WaveBox.ApiHandler.Handlers
{
	public class SearchApiHandler : IApiHandler
	{
		private IHttpProcessor Processor { get; set; }
		private UriWrapper Uri { get; set; }
		private User User { get; set; }

		/// <summary>
		/// Constructor for SearchApiHandler
		/// </summary>
		public SearchApiHandler(UriWrapper uri, IHttpProcessor processor, User user)
		{
			Processor = processor;
			Uri = uri;
			User = user;
		}

		/// <summary>
		/// Process performs a search for a query with specified types
		/// </summary>
		public void Process()
		{
			// Lists to return as results
			List<Artist> artists = new List<Artist>();
			List<Album> albums = new List<Album>();
			List<Song> songs = new List<Song>();
			List<Video> videos = new List<Video>();

			// If a query is provided...
			if (Uri.Parameters.ContainsKey("query"))
			{
				// URL decode to strip any URL-encoded characters
				string query = HttpUtility.UrlDecode(Uri.Parameters["query"]);

				// Ensure query is not blank
				if (query.Length > 0)
				{
					// Check for query field
					string field = null;
					if (Uri.Parameters.ContainsKey("field"))
					{
						// Use input field for query
						field = HttpUtility.UrlDecode(Uri.Parameters["field"]);
					}

					// Check for exact match parameter
					bool exact = false;
					if (Uri.Parameters.ContainsKey("exact"))
					{
						if (Uri.Parameters["exact"].IsTrue())
						{
							exact = true;
						}
					}

					// If a query type is provided...
					if (Uri.Parameters.ContainsKey("type"))
					{
						// Iterate all comma-separated values in query type
						foreach (string type in Uri.Parameters["type"].Split(','))
						{
							// Return results, populating lists depending on parameters specified
							switch (type)
							{
								case "artists":
									artists = Artist.SearchArtists(field, query, exact);
									break;
								case "albums":
									albums = Album.SearchAlbums(field, query, exact);
									break;
								case "songs":
									songs = Song.SearchSongs(field, query, exact);
									break;
								case "videos":
									videos = Video.SearchVideos(field, query, exact);
									break;
								default:
									artists = Artist.SearchArtists(field, query, exact);
									albums = Album.SearchAlbums(field, query, exact);
									songs = Song.SearchSongs(field, query, exact);
									videos = Video.SearchVideos(field, query, exact);
									break;
							}
						}
					}
					else
					{
						// For no type, provide all types of data
						artists = Artist.SearchArtists(field, query, exact);
						albums = Album.SearchAlbums(field, query, exact);
						songs = Song.SearchSongs(field, query, exact);
						videos = Video.SearchVideos(field, query, exact);
					}

					// On no results, return a 'harmless' error stating no results
					string error = null;
					if ((artists.Count == 0) && (albums.Count == 0) && (songs.Count == 0) && (videos.Count == 0))
					{
						error = "No search results found for query '" + query + "' on field '" + field + "'";
					}

					// Return all results
					string json = JsonConvert.SerializeObject(new SearchResponse(error, artists, albums, songs, videos), Injection.Kernel.Get<IServerSettings>().JsonFormatting);
					Processor.WriteJson(json);
				}
				else
				{
					// Return error JSON for empty query
					string json = JsonConvert.SerializeObject(new SearchResponse("Query cannot be empty", artists, albums, songs, videos), Injection.Kernel.Get<IServerSettings>().JsonFormatting);
					Processor.WriteJson(json);
				}
			}
			else
			{
				// Return error JSON for no query parameter
				string json = JsonConvert.SerializeObject(new SearchResponse("No search query provided", artists, albums, songs, videos), Injection.Kernel.Get<IServerSettings>().JsonFormatting);
				Processor.WriteJson(json);
			}
		}

		private class SearchResponse
		{
			[JsonProperty("error")]
			public string Error { get; set; }

			[JsonProperty("artists")]
			public List<Artist> Artists { get; set; }

			[JsonProperty("albums")]
			public List<Album> Albums { get; set; }

			[JsonProperty("songs")]
			public List<Song> Songs { get; set; }

			[JsonProperty("videos")]
			public List<Video> Videos { get; set; }

			public SearchResponse(string error, List<Artist> artists, List<Album> albums, List<Song> songs, List<Video> videos)
			{
				Error = error;
				Artists = artists;
				Albums = albums;
				Songs = songs;
				Videos = videos;
			}
		}
	}
}
