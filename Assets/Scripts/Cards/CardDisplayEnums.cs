// Enums the card face renders from. Copied unchanged (same names and ordinals) from Runeboard's
// Assets/Data/Enums so card JSON authored there deserializes here without remapping. The gameplay
// tables that used to sit alongside them (ArmyData's strength/defence dictionaries,
// ArmyTroopAbilityGroup) are combat logic, not presentation, and were deliberately left behind.

public enum RacesEnum
{
    Common = 0,
    Elf = 1,
    Dwarf = 2,
    Halfling = 3,
    Celestial = 4, 
    Orc = 5,
    Troll = 6, 
    Wraithlord = 7,
    Spider = 8,
    Dragon = 9,
    Emberfiend = 10,
    Undead = 11,
    Firstbloods = 12,
    Skinchanger = 13,
    Wildman = 14,
    Goblin = 15,
    Woldwarden = 16,
    Southerner = 17,
    Easterner = 18,
    Beast = 19,
    Machine = 20,
    Vampire = 21
}

public enum SexEnum
{
    Male = 0,
    Female = 1,
    Other = 2
}

public enum TroopsTypeEnum
{
    ma = 0, // Men-at-arms
    ar = 1, // Archers
    li = 2, // Light Infantry
    hi = 3, // Heavy Infantry
    lc = 4, // Light Cavalry
    hc = 5, // Heavy Cavalry
    ca = 6, // Siege Engines / Catapults
    ws = 7 // Warships / Navy
}

public enum ObjectCharacterArmySpecialAbilityEnum
{
    Ranged = 0, // Can attack flying creatures. Attacks first, and if the target unit does not die, then it takes damage.
    Mounted = 1, // Can attack same turn it's played. +1 to units that are tapped tapped 
    Poisoning = 2, // Chance of applying 1 turn of status effect Poisoned to the target enemy unit
    Pyromancer = 3, // Chance of applying 1 turn of status effect Burning to the target enemy unit. Double damage to siege engines, ships and ents
    Cursing = 4, // Chance of applying status effect Cursed to the target enemy unit
    Raiding = 5, // If an attack succeeds, steals 1 material of any type from the target owner, once per turn.
    Pikemen = 6, // +1 attack -1 defense against cavalry
    Shielded = 7, // If not tapped, forces to be selected before rest of unshielded units 
    Encouraging = 8, // Chance of applying status effect Encouraging to the units attacking at the same time with it
    Discouraging = 9, // Chance of applying status effect Discouraged to target enemy unit
    Berserker = 10, // Chance of causing same damage to another random target unit
    Charging = 11, // First time it attacks after is played, +2 attack -1 defense
    Fearsome = 12, // Chance of applying Fear status effect on the target unit
    Freezing = 13, // Chance of applying Frozen status effect on the target unit
    Blessing = 14, // Chance of applying Blessed status effect on the target unit. Double damage to undead and vampire units
    Hidden = 15, // Can not be targeted except by Rangers which remove the hidden status from them
    Slaughter = 18, // Chance of applying Bleeding status effect to the target unit
    Flying = 19, // Can only be targeted by Flying creatures, siege engines and Ranged    
    Sightseeing = 20, // Chance to reveal a random opponent card on successfull attack,
    BonusVersusDwarves = 21,
    BonusVersusElves = 22,
    BonusVersusOrcs = 23,
    BonusVersusTrolls = 24,
    
    BonusVersusWraithlords = 25,
    BonusVersusSpiders = 26,
    
    BonusVersusDragons = 27,
    BonusVersusUndeads = 28,
    BonusVersusHalfings = 29,
    
    BonusVersusGoblins = 30,
    BonusVersusSoutherners = 31,
    BonusVersusEasterners = 32,
    BonusVersusBeasts = 33,
    BonusVersusMachines = 34,
    BonusVersusFirstbloods = 35,
    BonusVersusSpirits = 36,
}


// Effects valid while active on a Character or Army.
public enum StatusEffects
{
    Poisoned = 0, // The damage inflicted when the status was applied is applied again divided by 2 (minimum: 1)
    Burning = 1, // 2 of damage inflicted next turn after the status is applied
    Cursed = 2, // Unity losses all ArmySpecialAbilityEnum
    Encouraged = 3, // +1 attack +1 defense
    Discouraged = 4, // -1 attack -1 defense
    Fear = 5, // Can't attack but can defend
    Frozen = 6, // Debuff to defense by a number equal to the damage they received when frozen was applied
    Blessed = 7, // Throws two dices when attacking instead of one, chooses highest result
    Bleeding = 8, // 25% of chance of getting double the damage fron an attack, once per turn
    Strenghtened = 10, // +2 to attack
    Fortified = 11, // +2 to defense
    Halted = 12 // Does not untap during the owner's draw stage.
}

public enum CharacterOnlySpecialAbilityEnum {
    Scholar = 0, // Bonux to play artifacts
    Lieutenant = 1, // Chance of applying Strenghtened status effect to the units attacking at the same time with it
    Shieldbearer = 2,  // Chance of applying Fortified status effect to the units attacking at the same time with it
    Ranger = 3, // Can attack Hidden units therefore revealing them
    Wormtongue = 4, // +1 to effectivity of emmissary skills
    Friendly = 5, // Bonus to recruit Factions and Allies
    Cruel = 6, // +1 to effectivity of agent skills
    Warlock = 7, // +1 to effectivity of mage spells and skills
    Leader = 8, // +1 to attack to own Common, Wildman and Skinchanger troops
    EasternerLeader = 9, // +1 to attack to own Easterlings troops, chance of applying BonusVersusFirstbloods to troops attacking at the same time with them
    SouthernerLeader = 10, // +1 to attack to own Southron troops, chance of applying BonusVersusFirstbloods to troops attacking at the same time with them
    OrcWarChief = 11, // +1 to attack to own Orc troops, chance of applying BonusVersusDwarves to troops attacking at the same time with them
    WarTroll = 12 // +1 to attack to own Troll troops, chance of applying BonusversusElves to troops attacking at the same time with them
    
}
