namespace BloodborneLauncher.Core;

public sealed record Trophy(int Id, string Name, string Description, Grade Grade, bool Dlc)
{
	public int Points => Grade switch
	{
		Grade.Platinum => 180, 
		Grade.Gold => 90, 
		Grade.Silver => 30, 
		_ => 15, 
	};
}
