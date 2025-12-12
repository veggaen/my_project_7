using Sandbox;
using System;
using System.Collections.Generic;

namespace Sandbox.Admin;

/// <summary>
/// HEX Permissions and Owner protection.
/// - Stores list of Owner SteamIDs
/// - Optional global bypass window for affecting owners (auto-expires)
/// Data file: data/hex/permissions.json
/// </summary>
public static class HexPermissions
{
    private const string FilePath = "hex/permissions.json";

    private class Data
    {
        public List<string> Owners { get; set; } = new();
        public DateTime OwnerBypassUntilUtc { get; set; } = DateTime.MinValue;
    }

    private static Data _data;
    private static bool _loaded;

    private static void EnsureLoaded()
    {
        if ( _loaded ) return;
        _loaded = true;

        if ( FileSystem.Data.FileExists( FilePath ) )
        {
            _data = FileSystem.Data.ReadJson<Data>( FilePath ) ?? new Data();
        }
        else
        {
            _data = new Data();
            Save();
        }
    }

    private static void Save()
    {
        FileSystem.Data.CreateDirectory( "hex" );
        FileSystem.Data.WriteJson( FilePath, _data );
    }

    public static bool IsOwnerSteamId( string steamId )
    {
        EnsureLoaded();
        if ( string.IsNullOrWhiteSpace( steamId ) ) return false;
        return _data.Owners.Contains( steamId );
    }

    public static void AddOwnerSteamId( string steamId )
    {
        EnsureLoaded();
        if ( string.IsNullOrWhiteSpace( steamId ) ) return;
        if ( !_data.Owners.Contains( steamId ) )
        {
            _data.Owners.Add( steamId );
            Save();
            Log.Info( $"[HEX] Added OWNER {steamId}" );
        }
    }

    public static void RemoveOwnerSteamId( string steamId )
    {
        EnsureLoaded();
        if ( _data.Owners.Remove( steamId ) ) Save();
    }

    /// <summary>
    /// Default: Owners are protected. When enabled, non-owners can target owners until expiry.
    /// </summary>
    public static void EnableOwnerBypass( TimeSpan duration )
    {
        EnsureLoaded();
        _data.OwnerBypassUntilUtc = DateTime.UtcNow.Add( duration );
        Save();
        Log.Info( $"[HEX] Owner bypass enabled for {duration.TotalHours:0.#}h (until {_data.OwnerBypassUntilUtc:u})" );
    }

    public static void DisableOwnerBypass()
    {
        EnsureLoaded();
        _data.OwnerBypassUntilUtc = DateTime.MinValue;
        Save();
    }

    public static bool IsOwnerBypassActive()
    {
        EnsureLoaded();
        return DateTime.UtcNow < _data.OwnerBypassUntilUtc;
    }

    public static bool IsTargetOwnerProtected( string targetSteamId )
    {
        EnsureLoaded();
        if ( !IsOwnerSteamId( targetSteamId ) ) return false;
        // Protected unless bypass is currently active
        return !IsOwnerBypassActive();
    }
}
