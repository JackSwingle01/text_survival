using text_survival.Actors;
using text_survival.IO;
using text_survival.Level;
using text_survival.Magic;

namespace text_survival.PlayerComponents;

class SpellManager
{
    public SpellManager(SkillRegistry skills)
    {
        _skills = skills;
        _spells.Add(SpellFactory.Bleeding);
        _spells.Add(SpellFactory.Poison);
        _spells.Add(SpellFactory.MinorHeal);
    }
    private readonly List<Spell> _spells = [];
    private readonly SkillRegistry _skills;
    public void SelectSpell(List<Actor> targets)
    {
        //get spell
        Output.WriteLine("Which spell would you like to cast?");
        var spell = Input.GetSelectionFromList(_spells, true);
        if (spell == null) return;

        // get target
        Output.WriteLine("Who would you like to cast ", spell.Name, " on?");
        var target = Input.GetSelectionFromList(targets, true);
        if (target == null) return;

        CastSpell(spell, target);
    }

    public void CastSpell(Spell spell, Actor target)
    {
        spell.Cast(target);
        _skills.AddExperience("Shamanism", 2);
    }
}