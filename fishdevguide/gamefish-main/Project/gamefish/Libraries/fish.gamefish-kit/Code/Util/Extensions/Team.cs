namespace GameFish;

partial class Library
{
	/// <summary>
	/// Set the team of each component on this object(which updates each object's tags).
	/// </summary>
	public static void SetTeam( this GameObject obj, in Team team, in FindMode findMode = FindMode.EverythingInSelf )
		=> Team.Set( obj, team, findMode );

	/// <returns> If this object is valid and tagged with that team. </returns>
	public static bool IsTeam( this GameObject obj, in ITeam team )
	{
		if ( obj?.Tags is null )
			return false;

		var teamTag = team?.Team?.Tag;

		if ( teamTag.IsBlank() )
			return false;

		return obj.Tags.Has( team.Team.Tag );
	}
}
