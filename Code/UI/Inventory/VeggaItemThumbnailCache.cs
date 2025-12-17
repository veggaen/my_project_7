using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Sandbox.UI;

/// <summary>
/// Client-side cache for runtime-rendered item thumbnails.
/// Used as a fallback when an item has no IconPath.
/// </summary>
public static class VeggaItemThumbnailCache
{
	const int DefaultSize = 128;
	const int MaxEntries = 256;
	const bool PersistToDisk = true;
	const string CacheFolder = "thumbs";

	static readonly object _lock = new();
	static readonly Dictionary<string, Texture> _modelThumbs = new( StringComparer.OrdinalIgnoreCase );
	static readonly HashSet<string> _failed = new( StringComparer.OrdinalIgnoreCase );

	public static Texture GetOrCreateModelThumbnail( string modelPath, int size = DefaultSize )
	{
		if ( string.IsNullOrWhiteSpace( modelPath ) )
			return null;

		modelPath = modelPath.Trim();

		lock ( _lock )
		{
			if ( _modelThumbs.TryGetValue( modelPath, out var cached ) )
				return cached;
			if ( _failed.Contains( modelPath ) )
				return null;

			// Simple safety valve to avoid unbounded growth.
			if ( _modelThumbs.Count >= MaxEntries )
			{
				_modelThumbs.Clear();
				_failed.Clear();
			}

			try
			{
				// Try disk cache first (per-client, persists across sessions).
				if ( PersistToDisk )
				{
					var cachePath = GetCachePath( modelPath, size );
					if ( FileSystem.Data.FileExists( cachePath ) )
					{
						var bytes = FileSystem.Data.ReadAllBytes( cachePath ).ToArray();
						using var fromDisk = Bitmap.CreateFromBytes( bytes );
						var diskTex = fromDisk?.ToTexture();
						if ( diskTex is not null )
						{
							_modelThumbs[modelPath] = diskTex;
							return diskTex;
						}
					}
				}

				var model = Model.Load( modelPath );
				if ( model is null )
				{
					_failed.Add( modelPath );
					return null;
				}

				// Render the model into a bitmap and convert to a texture.
				using var bitmap = new Bitmap( size, size );
				bitmap.Clear( Color.Transparent );
				SceneUtility.RenderModelBitmap( model, bitmap );

				var texture = bitmap.ToTexture();
				if ( texture is null )
				{
					_failed.Add( modelPath );
					return null;
				}

				// Save to disk for next run.
				if ( PersistToDisk )
				{
					try
					{
						var cachePath = GetCachePath( modelPath, size );
						var png = bitmap.ToPng();
						FileSystem.Data.CreateDirectory( CacheFolder );
						FileSystem.Data.WriteAllBytes( cachePath, png );
					}
					catch
					{
						// Ignore disk cache failures (still keep in-memory texture).
					}
				}

				_modelThumbs[modelPath] = texture;
				return texture;
			}
			catch
			{
				_failed.Add( modelPath );
				return null;
			}
		}
	}

	static string GetCachePath( string modelPath, int size )
	{
		// Stable filename: sha1(modelPath) + size to avoid collisions.
		var hashBytes = SHA1.HashData( Encoding.UTF8.GetBytes( modelPath ) );
		var hash = Convert.ToHexString( hashBytes ).ToLowerInvariant();
		return $"{CacheFolder}/{hash}_{size}.png";
	}
}
