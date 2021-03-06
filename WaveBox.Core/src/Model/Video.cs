using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Cirrious.MvvmCross.Plugins.Sqlite;
using Newtonsoft.Json;
using Ninject;
using TagLib;
using WaveBox.Core.Injected;
using WaveBox.Model;
using WaveBox.Static;

namespace WaveBox.Model
{
	public class Video : MediaItem
	{
		private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		public static readonly string[] ValidExtensions = { "m4v", "mp4", "mpg", "mkv", "avi" };

		[JsonIgnore, IgnoreRead, IgnoreWrite]
		public override ItemType ItemType { get { return ItemType.Video; } }

		[JsonProperty("itemTypeId"), IgnoreRead, IgnoreWrite]
		public override int ItemTypeId { get { return (int)ItemType; } }

		[JsonProperty("width")]
		public int? Width { get; set; }
		
		[JsonProperty("height")]
		public int? Height { get; set; }

		[JsonProperty("aspectRatio")]
		public float? AspectRatio
		{ 
			get 
			{
				if ((object)Width == null || (object)Height == null || Height == 0)
				{
					return null;
				}

				return (float)Width / (float)Height;
			}
		}

		public Video()
		{
		}
		
		public override void InsertMediaItem()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();
				conn.InsertLogged(this, InsertType.Replace);
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
			}

			Art.UpdateArtItemRelationship(ArtId, ItemId, true);
			Art.UpdateArtItemRelationship(ArtId, FolderId, false); // Only update a folder art relationship if it has no folder art
		}

		public static List<Video> AllVideos()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();
				return conn.Query<Video>("SELECT * FROM Video");
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
			}

			return new List<Video>();
		}

		public static int CountVideos()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();
				return conn.ExecuteScalar<int>("SELECT COUNT(ItemId) FROM Video");
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
			}

			return 0;
		}

		public static long TotalVideoSize()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();

				// Check if at least 1 video exists, to prevent exception if summing null
				int exists = conn.ExecuteScalar<int>("SELECT * FROM Video LIMIT 1");
				if (exists > 0)
				{
					return conn.ExecuteScalar<long>("SELECT SUM(FileSize) FROM Video");
				}
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
			}

			return 0;
		}

		public static long TotalVideoDuration()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();

				// Check if at least 1 video exists, to prevent exception if summing null
				int exists = conn.ExecuteScalar<int>("SELECT * FROM Video LIMIT 1");
				if (exists > 0)
				{
					return conn.ExecuteScalar<long>("SELECT SUM(Duration) FROM Video");
				}
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
			}

			return 0;
		}

		public static List<Video> SearchVideos(string field, string query, bool exact = true)
		{
			if (query == null)
			{
				return new List<Video>();
			}

			// Set default field, if none provided
			if (field == null)
			{
				field = "FileName";
			}

			// Check to ensure a valid query field was set
			if (!new string[] {"ItemId", "FolderId", "Duration", "Bitrate", "FileSize",
				"LastModified", "FileName", "Width", "Height", "FileType",
				"GenereId"}.Contains(field))
			{
				return new List<Video>();
			}

			ISQLiteConnection conn = null;
			try
			{
				conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();
				if (exact)
				{
					// Search for exact match
					return conn.Query<Video>("SELECT * FROM Video WHERE " + field + " = ? ORDER BY FileName", query);
				}
				else
				{
					// Search for fuzzy match (containing query)
					return conn.Query<Video>("SELECT * FROM Video WHERE " + field + " LIKE ? ORDER BY FileName", "%" + query + "%");
				}
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
			}

			return new List<Video>();
		}

		// Return a list of videos titled between a range of (a-z, A-Z, 0-9 characters)
		public static List<Video> RangeVideos(char start, char end)
		{
			// Ensure characters are alphanumeric, return empty list if either is not
			if (!Char.IsLetterOrDigit(start) || !Char.IsLetterOrDigit(end))
			{
				return new List<Video>();
			}

			string s = start.ToString();
			// Add 1 to character to make end inclusive
			string en = Convert.ToChar((int)end + 1).ToString();

			ISQLiteConnection conn = null;
			try
			{
				conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();

				List<Video> videos;
				videos = conn.Query<Video>("SELECT * FROM Video " +
										"WHERE Video.FileName BETWEEN LOWER(?) AND LOWER(?) " +
										"OR Video.FileName BETWEEN UPPER(?) AND UPPER(?)", s, en, s, en);

				videos.Sort(Video.CompareVideosByFileName);
				return videos;
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
			}

			// We had an exception somehow, so return an empty list
			return new List<Video>();
		}

		// Return a list of videos using SQL LIMIT x,y where X is starting index and Y is duration
		public static List<Video> LimitVideos(int index, int duration = Int32.MinValue)
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();

				// Begin building query
				List<Video> videos;

				string query = "SELECT * FROM Video LIMIT ? ";

				// Add duration to LIMIT if needed
				if (duration != Int32.MinValue && duration > 0)
				{
					query += ", ?";
				}

				// Run query, sort, send it back
				videos = conn.Query<Video>(query, index, duration);
				videos.Sort(Video.CompareVideosByFileName);
				return videos;
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
			}

			// We had an exception somehow, so return an empty list
			return new List<Video>();
		}

		public static int CompareVideosByFileName(Video x, Video y)
		{
			return x.FileName.CompareTo(y.FileName);
		}

		public new class Factory
		{
			public Video CreateVideo(int videoId)
			{
				ISQLiteConnection conn = null;
				try
				{
					conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();
					var result = conn.DeferredQuery<Video>("SELECT * FROM Video WHERE ItemId = ?", videoId);

					foreach (Video v in result)
					{
						return v;
					}
				}
				catch (Exception e)
				{
					logger.Error(e);
				}
				finally
				{
					Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
				}

				return new Video();
			}
		}
	}
}
