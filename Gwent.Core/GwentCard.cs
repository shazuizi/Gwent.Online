namespace Gwent.Core
{
	public class GwentCard
	{
		public string InstanceId { get; set; } = Guid.NewGuid().ToString();
		public string TemplateId { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;

		public FactionType Faction { get; set; } = FactionType.Neutral;
		public CardCategory Category { get; set; } = CardCategory.Unit;

		public int BaseStrength { get; set; }
		public int CurrentStrength { get; set; }

		public CardRow DefaultRow { get; set; } = CardRow.Melee;

		public bool IsHero { get; set; }

		public List<CardAbilityType> Abilities { get; set; } = new List<CardAbilityType>();

		/// <summary>
		/// Id grupy używane przez TightBond/Muster.
		/// </summary>
		public string? GroupId { get; set; }

		public bool HasAbility(CardAbilityType ability) => Abilities.Contains(ability);

		public GwentCard Clone()
		{
			return new GwentCard
			{
				InstanceId = Guid.NewGuid().ToString(),
				TemplateId = TemplateId,
				Name = Name,
				Faction = Faction,
				Category = Category,
				BaseStrength = BaseStrength,
				CurrentStrength = CurrentStrength,
				DefaultRow = DefaultRow,
				IsHero = IsHero,
				Abilities = new List<CardAbilityType>(Abilities),
				GroupId = GroupId
			};
		}
	}
}