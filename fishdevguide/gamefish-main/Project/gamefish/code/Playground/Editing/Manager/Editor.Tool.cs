namespace Playground;

partial class Editor
{
	[Property]
	[Title( "Active" )]
	[Feature( EDITOR ), Group( MODE ), Order( TOOL_ORDER )]
	public EditorTool Tool
	{
		get => _mode;
		protected set
		{
			var old = _mode;
			_mode = value;

			OnToolSet( _mode, old );
		}
	}

	protected EditorTool _mode;

	public virtual bool TrySetTool( EditorTool tool )
	{
		if ( !tool.IsValid() )
		{
			Tool = null;
			return true;
		}

		if ( !tool.IsClientAllowed( Client.Local ) )
			return false;

		Tool = tool;

		return true;
	}

	protected virtual void OnToolSet( EditorTool newTool, EditorTool oldTool )
	{
		if ( oldTool.IsValid() )
			oldTool.OnExit();

		if ( newTool.IsValid() )
			newTool.OnEnter();
	}

	protected virtual void SimulateTool( in float deltaTime, bool isFixedUpdate )
	{
		if ( !Tool.IsValid() )
			return;

		if ( isFixedUpdate )
			Tool.FixedSimulate( deltaTime );
		else
			Tool.FrameSimulate( deltaTime );
	}

	/// <returns> If the default editor behavior should be prevented. </returns>
	public virtual void OnLeftClick()
	{
		// this.Log( "Left clicked." );

		if ( Tool.IsValid() && Tool.TryLeftClick() )
			return;
	}

	/// <returns> If the default editor behavior should be prevented. </returns>
	public virtual void OnRightClick()
	{
		// this.Log( "Right clicked." );

		if ( Tool.IsValid() && Tool.TryRightClick() )
			return;
	}

	/// <returns> If the default editor behavior should be prevented. </returns>
	public virtual void OnMiddleClick()
	{
		// this.Log( "Middle clicked." );

		if ( Tool.IsValid() && Tool.TryMiddleClick() )
			return;
	}

	/// <returns> If the default editor behavior should be prevented. </returns>
	public virtual void OnMouseWheel( in Vector2 dir )
	{
		// this.Log( $"Mouse wheel:[{dir}]" );

		if ( Tool.IsValid() && Tool.TryMouseWheel( in dir ) )
			return;
	}

	public virtual void OnMouseUp( in MouseButtons mb )
	{
		// this.Log( $"Mouse up:[{mb}]" );

		if ( Tool.IsValid() )
			Tool.OnMouseUp( in mb );
	}
}
