using System.Text.Json.Nodes;
namespace Herohold.Web.Services;
// Reviewed fictional fixture; never reads a player save.
public static class SampleCanonical
{
 public static JsonObject Create()=>JsonNode.Parse(Data)!.AsObject();
 private const string Data="""
{
  "schema_version": 5,
  "character_id": "veyra-thorne",
  "name": "Veyra Thorne",
  "level": 5,
  "class_levels": {
    "Ranger": 5
  },
  "notes": "",
  "species": "Elf",
  "species_traits": [
    "Darkvision",
    "Elven Lineage",
    "Fey Ancestry",
    "Keen Senses",
    "Trance"
  ],
  "size": "Medium",
  "background": "Wayfarer",
  "background_feature": "Origin Feat: Lucky",
  "class": "Ranger",
  "subclass": "Gloom Stalker",
  "ability_scores": {
    "Strength": 12,
    "Dexterity": 18,
    "Constitution": 14,
    "Intelligence": 10,
    "Wisdom": 16,
    "Charisma": 10
  },
  "base_ability_scores": {
    "Strength": 12,
    "Dexterity": 16,
    "Constitution": 14,
    "Intelligence": 10,
    "Wisdom": 14,
    "Charisma": 10
  },
  "ability_score_bonuses": {
    "Strength": 0,
    "Dexterity": 2,
    "Constitution": 0,
    "Intelligence": 0,
    "Wisdom": 2,
    "Charisma": 0
  },
  "ability_modifiers": {
    "Strength": 1,
    "Dexterity": 4,
    "Constitution": 2,
    "Intelligence": 0,
    "Wisdom": 4,
    "Charisma": 0
  },
  "ability_score_method": "Manual entry",
  "ability_score_assignments": {},
  "rolled_ability_scores": [],
  "proficiency_bonus": 3,
  "saving_throw_proficiencies": [
    "Strength",
    "Dexterity"
  ],
  "skill_proficiencies": [
    "Insight",
    "Stealth",
    "Perception",
    "Athletics",
    "Nature",
    "Survival",
    "Acrobatics"
  ],
  "expertise": [
    "Perception",
    "Stealth"
  ],
  "saving_throw_bonuses": {
    "Strength": 4,
    "Dexterity": 7,
    "Constitution": 2,
    "Intelligence": 0,
    "Wisdom": 4,
    "Charisma": 0
  },
  "skill_bonuses": {
    "Acrobatics": 7,
    "Animal Handling": 4,
    "Arcana": 0,
    "Athletics": 4,
    "Deception": 0,
    "History": 0,
    "Insight": 7,
    "Intimidation": 0,
    "Investigation": 0,
    "Medicine": 4,
    "Nature": 3,
    "Perception": 10,
    "Performance": 0,
    "Persuasion": 0,
    "Religion": 0,
    "Sleight of Hand": 4,
    "Stealth": 10,
    "Survival": 7
  },
  "armor_proficiencies": [
    "Light Armor",
    "Medium Armor",
    "Shields"
  ],
  "weapon_proficiencies": [
    "Martial Weapons",
    "Simple Weapons"
  ],
  "tool_proficiencies": [
    "Thieves' Tools"
  ],
  "languages": [
    "Elvish",
    "Dwarvish"
  ],
  "features": [
    "Darkvision",
    "Elven Lineage",
    "Fey Ancestry",
    "Keen Senses",
    "Trance",
    "Origin Feat: Lucky",
    "Lucky",
    "Skill Expert",
    "Drow",
    "Dancing Lights",
    "Faerie Fire",
    "Darkness",
    "Disguise Self",
    "Rope Trick",
    "Spellcasting",
    "Favored Enemy",
    "Weapon Mastery",
    "Deft Explorer",
    "Fighting Style",
    "Ranger Subclass",
    "Ability Score Improvement",
    "Extra Attack",
    "Dread Ambusher",
    "Gloom Stalker",
    "Gloom Stalker Spells",
    "Umbral Sight",
    "Archery"
  ],
  "pending_choices": [],
  "feature_configurations": [],
  "level_history": [],
  "completed_level_up_transactions": [],
  "hit_dice": {
    "Ranger": 5
  },
  "hp_rolls": [],
  "hp_history": [
    {
      "character_level": 1,
      "class_name": "Ranger",
      "method": "maximum",
      "base_gain": 10
    },
    {
      "character_level": 2,
      "class_name": "Ranger",
      "method": "fixed",
      "base_gain": 6
    },
    {
      "character_level": 3,
      "class_name": "Ranger",
      "method": "fixed",
      "base_gain": 6
    },
    {
      "character_level": 4,
      "class_name": "Ranger",
      "method": "fixed",
      "base_gain": 6
    },
    {
      "character_level": 5,
      "class_name": "Ranger",
      "method": "fixed",
      "base_gain": 6
    }
  ],
  "class_options": {
    "class_skills": [
      "Athletics",
      "Nature",
      "Survival"
    ],
    "ranger_deft_explorer_expertise": [
      "Perception"
    ],
    "fighting_style": "Archery",
    "ranger_languages": [
      "Elvish",
      "Dwarvish"
    ]
  },
  "species_options": {
    "skill_proficiencies": [
      "Perception"
    ],
    "lineage": "Drow",
    "lineage_spellcasting_ability": "Wisdom",
    "lineage_spells": [
      "Dancing Lights",
      "Faerie Fire",
      "Darkness"
    ]
  },
  "background_options": {
    "primary_ability": "Dexterity",
    "secondary_ability": "Wisdom"
  },
  "origin_feats": [
    "Lucky"
  ],
  "general_feats": [
    {
      "instance_id": "sample-skill-expert",
      "feat_id": "skill_expert_xphb",
      "name": "Skill Expert",
      "source": {
        "type": "class_level",
        "class_name": "Ranger",
        "level": 4
      },
      "choices": {
        "ability": "Wisdom",
        "skill_proficiency": "Acrobatics",
        "expertise": "Stealth"
      }
    }
  ],
  "ability_score_adjustments": [
    {
      "type": "ability_score_adjustment",
      "source_type": "feat",
      "source_feat": "skill_expert_xphb",
      "source_name": "Skill Expert",
      "adjustments": {
        "Wisdom": 1
      },
      "maximum": 20
    }
  ],
  "derived_resources": {
    "favored_enemy": 3,
    "weapon_mastery_count": 2,
    "spell_slots": {
      "1": 4,
      "2": 2
    },
    "proficiency_bonus": 3,
    "luck_points": 3,
    "spellcasting_ability": "Wisdom",
    "spell_save_dc": 15,
    "spell_attack_bonus": 7
  },
  "weapon_masteries": [
    "Longbow",
    "Shortsword"
  ],
  "origin_feat_choices": {
    "skills": [],
    "tools": [],
    "cantrips": [],
    "spell": "",
    "spellcasting_ability": ""
  },
  "spells": {
    "cantrips": [],
    "prepared": [
      "Cure Wounds",
      "Fog Cloud",
      "Jump",
      "Hail of Thorns",
      "Pass without Trace",
      "Lesser Restoration"
    ],
    "known": [],
    "spellbook": []
  },
  "include_default_equipment": false,
  "custom_equipment": [
    "Studded Leather Armor",
    "Longbow",
    "Shortsword",
    "Wayfinder charm"
  ],
  "equipment_modifier_bonuses": {
    "Wayfinder charm": {
      "Wisdom": 1
    }
  },
  "equipment": [
    "Studded Leather Armor",
    "Longbow",
    "Shortsword",
    "Wayfinder charm"
  ],
  "equipment_migration_pending": false,
  "presentation_preferences": {
    "primary_weapon": "",
    "featured_cantrip": "",
    "weapon_wielding_modes": {}
  },
  "import_provenance": {
    "warnings": []
  },
  "import_overrides": {
    "maximum_hp_adjustment": 0
  },
  "pdf_passthrough_fields": {},
  "hit_points": {
    "max": 44,
    "maximum": 44,
    "current": 32,
    "temporary": 0,
    "hit_die": "d10"
  },
  "armor_class": 16,
  "initiative": 8,
  "speed": 30,
  "folio_species_auto_grants": [
    "Dancing Lights",
    "Faerie Fire",
    "Darkness"
  ],
  "ability_score_adjustment_bonuses": {
    "Strength": 0,
    "Dexterity": 0,
    "Constitution": 0,
    "Intelligence": 0,
    "Wisdom": 1,
    "Charisma": 0
  },
  "ability_modifier_equipment_bonuses": {
    "Strength": 0,
    "Dexterity": 0,
    "Constitution": 0,
    "Intelligence": 0,
    "Wisdom": 1,
    "Charisma": 0
  },
  "speed_details": {
    "base": 30,
    "value": 30,
    "modifiers": []
  },
  "speed_modifiers": [],
  "spellcasting_ability": "Wisdom",
  "subclass_granted_spells": [
    "Disguise Self",
    "Rope Trick"
  ],
  "feat_spell_grant_details": [],
  "class_option_spell_grant_details": [],
  "subclass_spell_grant_details": [
    {
      "name": "Disguise Self",
      "level_gained": 3,
      "source": {
        "type": "subclass",
        "subclass_id": "gloom_stalker_xphb",
        "subclass_name": "Gloom Stalker",
        "source": "XPHB"
      },
      "spellcasting_ability": "Wisdom",
      "grant_type": "always_prepared",
      "always_prepared": true,
      "counts_against_limit": false
    },
    {
      "name": "Rope Trick",
      "level_gained": 5,
      "source": {
        "type": "subclass",
        "subclass_id": "gloom_stalker_xphb",
        "subclass_name": "Gloom Stalker",
        "source": "XPHB"
      },
      "spellcasting_ability": "Wisdom",
      "grant_type": "always_prepared",
      "always_prepared": true,
      "counts_against_limit": false
    }
  ],
  "spell_grant_details": [
    {
      "name": "Disguise Self",
      "level_gained": 3,
      "source": {
        "type": "subclass",
        "subclass_id": "gloom_stalker_xphb",
        "subclass_name": "Gloom Stalker",
        "source": "XPHB"
      },
      "spellcasting_ability": "Wisdom",
      "grant_type": "always_prepared",
      "always_prepared": true,
      "counts_against_limit": false
    },
    {
      "name": "Rope Trick",
      "level_gained": 5,
      "source": {
        "type": "subclass",
        "subclass_id": "gloom_stalker_xphb",
        "subclass_name": "Gloom Stalker",
        "source": "XPHB"
      },
      "spellcasting_ability": "Wisdom",
      "grant_type": "always_prepared",
      "always_prepared": true,
      "counts_against_limit": false
    }
  ],
  "spellcasting_sources": [
    {
      "source_name": "Ranger Spellcasting",
      "source_type": "class",
      "ability": "Wisdom",
      "spell_save_dc": 15,
      "spell_attack_bonus": 7
    },
    {
      "source_name": "Gloom Stalker",
      "source_type": "subclass",
      "ability": "Wisdom",
      "spell_save_dc": 15,
      "spell_attack_bonus": 7
    }
  ],
  "savant_spell_records": [],
  "subclass_known_spells": [],
  "feature_descriptions": {
    "Ability Score Improvement": "Rules text comes from your local rules data.",
    "Actor": "Rules text comes from your local rules data.",
    "Alert": "Rules text comes from your local rules data.",
    "Archery": "You gain a +2 bonus to attack rolls you make with Ranged weapons.",
    "Athlete": "Rules text comes from your local rules data.",
    "Blessed Warrior": "Rules text comes from your local rules data.",
    "Blind Fighting": "You have Blindsight with a range of 10 feet.",
    "Boon of Combat Prowess": "Rules text comes from your local rules data.",
    "Boon of Dimensional Travel": "Rules text comes from your local rules data.",
    "Boon of Energy Resistance": "Rules text comes from your local rules data.",
    "Boon of Fate": "Rules text comes from your local rules data.",
    "Boon of Fortitude": "Rules text comes from your local rules data.",
    "Boon of Irresistible Offense": "Rules text comes from your local rules data.",
    "Boon of Recovery": "Rules text comes from your local rules data.",
    "Boon of Skill": "Rules text comes from your local rules data.",
    "Boon of Speed": "Rules text comes from your local rules data.",
    "Boon of Spell Recall": "Rules text comes from your local rules data.",
    "Boon of the Night Spirit": "Rules text comes from your local rules data.",
    "Boon of Truesight": "Rules text comes from your local rules data.",
    "Charger": "Rules text comes from your local rules data.",
    "Chef": "Rules text comes from your local rules data.",
    "Crafter": "Rules text comes from your local rules data.",
    "Crossbow Expert": "Rules text comes from your local rules data.",
    "Crusher": "Rules text comes from your local rules data.",
    "Defense": "Rules text comes from your local rules data.",
    "Defensive Duelist": "Rules text comes from your local rules data.",
    "Druidic Warrior": "Rules text comes from your local rules data.",
    "Dual Wielder": "Rules text comes from your local rules data.",
    "Dueling": "Rules text comes from your local rules data.",
    "Durable": "Rules text comes from your local rules data.",
    "Elemental Adept": "Rules text comes from your local rules data.",
    "Fey-Touched": "Rules text comes from your local rules data.",
    "Grappler": "Rules text comes from your local rules data.",
    "Great Weapon Fighting": "Rules text comes from your local rules data.",
    "Great Weapon Master": "Rules text comes from your local rules data.",
    "Healer": "Rules text comes from your local rules data.",
    "Heavily Armored": "Rules text comes from your local rules data.",
    "Heavy Armor Master": "Rules text comes from your local rules data.",
    "Inspiring Leader": "Rules text comes from your local rules data.",
    "Interception": "Rules text comes from your local rules data.",
    "Keen Mind": "Rules text comes from your local rules data.",
    "Lightly Armored": "Rules text comes from your local rules data.",
    "Lucky": "Rules text comes from your local rules data.",
    "Mage Slayer": "Rules text comes from your local rules data.",
    "Magic Initiate": "Rules text comes from your local rules data.",
    "Martial Weapon Training": "Rules text comes from your local rules data.",
    "Medium Armor Master": "Rules text comes from your local rules data.",
    "Moderately Armored": "Rules text comes from your local rules data.",
    "Mounted Combatant": "Rules text comes from your local rules data.",
    "Musician": "Rules text comes from your local rules data.",
    "Observant": "Rules text comes from your local rules data.",
    "Piercer": "Rules text comes from your local rules data.",
    "Poisoner": "Rules text comes from your local rules data.",
    "Polearm Master": "Rules text comes from your local rules data.",
    "Protection": "Rules text comes from your local rules data.",
    "Resilient": "Rules text comes from your local rules data.",
    "Ritual Caster": "Rules text comes from your local rules data.",
    "Savage Attacker": "Rules text comes from your local rules data.",
    "Sentinel": "Rules text comes from your local rules data.",
    "Shadow-Touched": "Rules text comes from your local rules data.",
    "Sharpshooter": "Rules text comes from your local rules data.",
    "Shield Master": "Rules text comes from your local rules data.",
    "Skill Expert": "Rules text comes from your local rules data.",
    "Skilled": "Rules text comes from your local rules data.",
    "Skulker": "Rules text comes from your local rules data.",
    "Slasher": "Rules text comes from your local rules data.",
    "Speedy": "Rules text comes from your local rules data.",
    "Spell Sniper": "Rules text comes from your local rules data.",
    "Tavern Brawler": "Rules text comes from your local rules data.",
    "Telekinetic": "Rules text comes from your local rules data.",
    "Telepathic": "Rules text comes from your local rules data.",
    "Thrown Weapon Fighting": "Rules text comes from your local rules data.",
    "Tough": "Rules text comes from your local rules data.",
    "Two-Weapon Fighting": "Rules text comes from your local rules data.",
    "Unarmed Fighting": "Rules text comes from your local rules data.",
    "War Caster": "Rules text comes from your local rules data.",
    "Weapon Master": "Rules text comes from your local rules data.",
    "Darkvision": "You have Darkvision with a range of 60 feet.",
    "Elven Lineage": "Rules text comes from your local rules data.",
    "Fey Ancestry": "Rules text comes from your local rules data.",
    "Keen Senses": "You have proficiency in the Insight, Perception, or Survival skill.",
    "Trance": "Rules text comes from your local rules data.",
    "Origin Feat: Lucky": "Rules text comes from your local rules data.",
    "Favored Enemy": "Rules text comes from your local rules data.",
    "Spellcasting": "Rules text comes from your local rules data.",
    "Weapon Mastery": "Rules text comes from your local rules data.",
    "Deft Explorer": "Rules text comes from your local rules data.",
    "Fighting Style": "Rules text comes from your local rules data.",
    "Ranger Subclass": "Rules text comes from your local rules data.",
    "Extra Attack": "Rules text comes from your local rules data.",
    "Roving": "Rules text comes from your local rules data.",
    "Subclass Feature": "You gain a feature from your Ranger Subclass.",
    "Expertise": "Rules text comes from your local rules data.",
    "Tireless": "Rules text comes from your local rules data.",
    "Relentless Hunter": "Taking damage can't break your Concentration on Hunter's Mark.",
    "Nature's Veil": "Rules text comes from your local rules data.",
    "Precise Hunter": "Rules text comes from your local rules data.",
    "Feral Senses": "Rules text comes from your local rules data.",
    "Epic Boon": "Rules text comes from your local rules data.",
    "Foe Slayer": "The damage die of your Hunter's Mark is a d10 rather than a d6.",
    "Dread Ambusher": "Rules text comes from your local rules data.",
    "Gloom Stalker": "Rules text comes from your local rules data.",
    "Gloom Stalker Spells": "Rules text comes from your local rules data.",
    "Umbral Sight": "Rules text comes from your local rules data.",
    "Iron Mind": "Rules text comes from your local rules data.",
    "Stalker's Flurry": "Rules text comes from your local rules data.",
    "Shadowy Dodge": "Rules text comes from your local rules data."
  },
  "feature_related_features": {
    "Gloom Stalker": [
      {
        "id": "ranger_gloom_stalker_3_dread_ambusher",
        "name": "Dread Ambusher",
        "cost": "",
        "source": "XPHB",
        "level": 3,
        "description": "Rules text comes from your local rules data."
      },
      {
        "id": "ranger_gloom_stalker_3_gloom_stalker_spells",
        "name": "Gloom Stalker Spells",
        "cost": "",
        "source": "XPHB",
        "level": 3,
        "description": "Rules text comes from your local rules data."
      },
      {
        "id": "ranger_gloom_stalker_3_umbral_sight",
        "name": "Umbral Sight",
        "cost": "",
        "source": "XPHB",
        "level": 3,
        "description": "Rules text comes from your local rules data."
      }
    ]
  },
  "hit_point_details": {
    "calculated_before_import_adjustment": 44,
    "import_adjustment": 0,
    "maximum": 44
  },
  "senses": [],
  "damage_resistances": [],
  "armor_class_details": {
    "value": 16,
    "formula_id": "studded_leather_armor",
    "source": "armor",
    "source_name": "Studded Leather Armor",
    "shield_applied": false,
    "medium_armor_dexterity_cap": 2,
    "candidates": [
      {
        "id": "unarmored",
        "name": "Unarmored",
        "base_value": 14,
        "source": "base",
        "allows_shield": true,
        "value": 14,
        "shield_applied": false
      },
      {
        "id": "studded_leather_armor",
        "name": "Studded Leather Armor",
        "base_value": 16,
        "source": "armor",
        "allows_shield": true,
        "value": 16,
        "shield_applied": false
      }
    ]
  },
  "spell_metadata": {
    "Cure Wounds": {
      "level": 1,
      "school": "Abjuration",
      "casting_time": "1 action",
      "range": "Touch",
      "duration": "Instantaneous"
    },
    "Fog Cloud": {
      "level": 1,
      "school": "Conjuration",
      "casting_time": "1 action",
      "range": "120 feet",
      "duration": "Concentration, up to 1 hour"
    },
    "Jump": {
      "level": 1,
      "school": "Transmutation",
      "casting_time": "1 bonus",
      "range": "Touch",
      "duration": "1 minute"
    },
    "Hail of Thorns": {
      "level": 1,
      "school": "Conjuration",
      "casting_time": "1 bonus",
      "range": "Self",
      "duration": "Instantaneous"
    },
    "Pass without Trace": {
      "level": 2,
      "school": "Abjuration",
      "casting_time": "1 action",
      "range": "30 feet",
      "duration": "Concentration, up to 1 hour"
    },
    "Lesser Restoration": {
      "level": 2,
      "school": "Abjuration",
      "casting_time": "1 bonus",
      "range": "Touch",
      "duration": "Instantaneous"
    },
    "Dancing Lights": {
      "level": 0,
      "school": "Illusion",
      "casting_time": "1 action",
      "range": "120 feet",
      "duration": "Concentration, up to 1 minute"
    },
    "Faerie Fire": {
      "level": 1,
      "school": "Evocation",
      "casting_time": "1 action",
      "range": "60 feet",
      "duration": "Concentration, up to 1 minute"
    },
    "Darkness": {
      "level": 2,
      "school": "Evocation",
      "casting_time": "1 action",
      "range": "60 feet",
      "duration": "Concentration, up to 10 minute"
    },
    "Disguise Self": {
      "level": 1,
      "school": "Illusion",
      "casting_time": "1 action",
      "range": "Self",
      "duration": "1 hour"
    },
    "Rope Trick": {
      "level": 2,
      "school": "Transmutation",
      "casting_time": "1 action",
      "range": "Touch",
      "duration": "1 hour"
    }
  },
  "spell_descriptions": {
    "Cure Wounds": "Rules text comes from your local rules data.",
    "Fog Cloud": "Rules text comes from your local rules data.",
    "Jump": "Rules text comes from your local rules data.",
    "Hail of Thorns": "Rules text comes from your local rules data.",
    "Pass without Trace": "Rules text comes from your local rules data.",
    "Lesser Restoration": "Rules text comes from your local rules data.",
    "Dancing Lights": "Rules text comes from your local rules data.",
    "Faerie Fire": "Rules text comes from your local rules data.",
    "Darkness": "Rules text comes from your local rules data.",
    "Disguise Self": "Rules text comes from your local rules data.",
    "Rope Trick": "Rules text comes from your local rules data."
  }
}
""";
}
