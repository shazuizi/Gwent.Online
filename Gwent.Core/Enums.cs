namespace Gwent.Core
{
	public enum CardType
	{
		Unit,
		Weather,
		Special
	}

	public enum Row
	{
		None,
		Melee,
		Ranged,
		Siege
	}

	public enum Ability
	{
		None,
		Spy,
		TightBond,
		Morale,
		Medic
	}

	public enum GamePhase
	{
		WaitingForPlayers,
		Mulligan,
		Playing,
		RoundEnd,
		GameEnd
	}
}
