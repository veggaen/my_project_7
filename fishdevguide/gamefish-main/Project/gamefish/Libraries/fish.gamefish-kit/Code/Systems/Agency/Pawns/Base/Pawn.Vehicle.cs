using System.Text.Json.Serialization;
using Sandbox.Services;

namespace GameFish;

partial class Pawn
{
	/// <summary>
	/// The comfy chair we're sitting in.
	/// </summary>
	[Title( "Seat" )]
	[Property, JsonIgnore]
	[Feature( PAWN ), Group( VEHICLE )]
	protected Seat InspectorSeat
	{
		get => Seat;
		set => Seat = value;
	}

	[Sync( SyncFlags.FromHost )]
	public Seat Seat
	{
		get => _seat;
		set
		{
			if ( _seat == value )
				return;

			var old = _seat;
			_seat = value;

			Tags?.Set( TAG_SITTING, _seat.IsValid() );

			OnSetSeat( _seat, old );
		}
	}

	protected Seat _seat;

	protected virtual void OnSetSeat( Seat newSeat, Seat oldSeat )
	{
		FollowSeat( newSeat );
	}

	public virtual void OnSeatMoved( Seat seat )
	{
		FollowSeat( seat );
	}

	/// <summary>
	/// Glue they ass onto the seat.
	/// </summary>
	protected virtual void FollowSeat( Seat seat )
	{
		if ( IsProxy || !seat.IsValid() )
			return;

		// TEMP!
		WorldPosition = seat.WorldPosition;
		// WorldRotation = seat.WorldRotation;
	}

	protected virtual void SimulateSeat( in float deltaTime, in bool isFixedUpdate )
	{
		if ( !Seat.IsValid() )
		{
			if ( Tags.Has( TAG_SITTING ) )
				Tags.Remove( TAG_SITTING );

			return;
		}

		if ( !Tags.Has( TAG_SITTING ) )
			Tags.Add( TAG_SITTING );

		Seat.Simulate( in deltaTime, in isFixedUpdate );
	}
}
