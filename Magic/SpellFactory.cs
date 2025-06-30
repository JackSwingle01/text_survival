using text_survival.Effects;

namespace text_survival.Magic
{
  public static class SpellFactory
  {
    public static Spell MinorHeal => new Spell("Minor Heal",
                                                  EffectBuilderExtensions
                                                    .CreateEffect("bleed spell")
                                                    .Healing(10)
                                                    .Build(),
                                                  true);
    public static Spell Bleeding => new Spell("Bleeding",
                                              EffectBuilderExtensions
                                                .CreateEffect("bleed spell")
                                                .Bleeding(10)
                                                .WithDuration(60)
                                                .Build(),
                                              false);
    public static Spell Poison => new Spell("Poison",
                                              EffectBuilderExtensions
                                                .CreateEffect("poison spell")
                                                .Poisoned(5)
                                                .WithDuration(180)
                                                .Build(),
                                              false);
  }
}
