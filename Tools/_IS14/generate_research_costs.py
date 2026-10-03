"""Generates Resources/Prototypes/_IS14/Research/costs.yml from the existing technology
prototypes: upstream prices, rescaled and split across the five IS14 currencies."""
import io, glob, re, collections

SCI, IND, MIL, BIO, SOC = 'Science', 'Industrial', 'Military', 'Biological', 'Social'

BANDS = {1: (15, 40), 2: (50, 120), 3: (150, 300)}
SPLITS = {1: (1.0,), 2: (0.7, 0.3), 3: (0.55, 0.3, 0.15)}

BIO_WORDS = ('med', 'surg', 'clon', 'cyber', 'augment', 'autodoc', 'treatment', 'stasis',
             'medipen', 'hydroponic', 'meat', 'critter', 'rescue', 'biochem', 'xeno',
             'plumbing')
SOCIAL_WORDS = ('entertain', 'honk', 'horn', 'clean', 'translat', 'communicat', 'radio',
                'accessib', 'astro', 'spray', 'belt', 'audio')
ROBOT_WORDS = ('robotic', 'mech', 'ripley', 'gygax', 'durand', 'clarke', 'aplu', 'modsuit')
BLUESPACE_WORDS = ('bluespace', 'quantum', 'leaping')


def profile(tech_id, discipline):
    """Returns the currencies for a technology, most important first."""
    low = tech_id.lower()

    if discipline == 'Arsenal':
        if any(w in low for w in BLUESPACE_WORDS):
            return [MIL, SCI, IND]
        if any(w in low for w in ROBOT_WORDS):
            return [MIL, IND, SCI]
        return [MIL, IND, SCI]

    if discipline == 'Industrial':
        if any(w in low for w in BLUESPACE_WORDS):
            return [SCI, IND, MIL]
        if any(w in low for w in ROBOT_WORDS):
            return [IND, SCI, MIL]
        return [IND, SCI, MIL]

    if discipline == 'Experimental':
        if 'xeno' in low or 'biolog' in low:
            return [SCI, BIO, IND]
        if any(w in low for w in ROBOT_WORDS):
            return [IND, SCI, SOC]
        return [SCI, IND, BIO]

    # CivilianServices: the department that actually splits in two.
    if any(w in low for w in BIO_WORDS):
        return [BIO, IND, SCI]
    if any(w in low for w in SOCIAL_WORDS):
        return [SOC, IND, SCI]
    return [SOC, BIO, IND]


def round5(value):
    return max(5, int(round(value / 5.0)) * 5)


# Showcase cross-branch technologies, priced by hand. They are the point of the whole
# system: robotics needs a machine shop *and* a theory, cloning needs tissue *and* a printer.
OVERRIDES = {
    'BasicRobotics': {IND: 30, SCI: 15},
    'AdvancedRobotics': {IND: 70, SCI: 45, SOC: 15},
    'Xenobiology': {SCI: 25, BIO: 15},
    'Cloning': {BIO: 140, IND: 70, SCI: 40},
    'BasicCybernetics': {IND: 45, BIO: 35},
    'AdvancedCybernetics': {IND: 90, BIO: 70, SCI: 40},
    'CombatAugmentation': {BIO: 45, MIL: 35, IND: 20},
    'HighEndSurgery': {BIO: 130, SCI: 55, IND: 25},
    'BluespaceTheory': {SCI: 40},
    'ExplosiveTechnology': {MIL: 35},
    'BasicAnomalousResearch': {SCI: 30},
    'AtmosphericTech': {IND: 30},
    'PowerGeneration': {IND: 35},
    'Hydroponics': {BIO: 25},
    'AdvancedEntertainment': {SOC: 30},
    'BasicTranslation': {SOC: 55, SCI: 25},
    'AdvancedTranslation': {SOC: 130, SCI: 65, IND: 30},
}


def tier_scale(techs):
    """Maps upstream prices onto our bands per tier, so relative balance survives."""
    ranges = {}
    for tier in (1, 2, 3):
        prices = [t['cost'] for t in techs if min(3, max(1, t['tier'])) == tier]
        ranges[tier] = (min(prices), max(prices)) if prices else (0, 1)
    return ranges


def parse():
    files = sorted(glob.glob('Resources/Prototypes/Research/*.yml')
                   + [p for p in glob.glob('Resources/Prototypes/_*/Research/*.yml')
                      if not p.endswith('technologies.yml')])  # the IS14 tree prices itself
    techs = []
    for path in files:
        text = io.open(path, encoding='utf-8').read()
        for block in re.split(r'(?m)^- type:\s*', text)[1:]:
            # The kind can carry a trailing comment: '- type: technology  # Goobstation'.
            if block.split('\n', 1)[0].split('#')[0].strip() != 'technology':
                continue

            def field(name):
                m = re.search(r'(?m)^  %s:\s*([^\n#]+)' % name, block)
                return m.group(1).strip() if m else None

            tech_id = field('id')
            if tech_id is None:
                continue

            techs.append(dict(
                id=tech_id,
                discipline=field('discipline') or 'Experimental',
                tier=int(field('tier') or 1),
                cost=int(field('cost') or 10000),
                file=path,
            ))
    return techs


def costs_for(tech, ranges):
    if tech['id'] in OVERRIDES:
        return collections.OrderedDict(OVERRIDES[tech['id']])

    tier = min(3, max(1, tech['tier']))
    low, high = BANDS[tier]
    cheapest, dearest = ranges[tier]

    span = max(1, dearest - cheapest)
    position = (tech['cost'] - cheapest) / float(span)
    total = round5(low + position * (high - low))

    currencies = profile(tech['id'], tech['discipline'])
    split = SPLITS[tier]

    result = collections.OrderedDict()
    for share, currency in zip(split, currencies):
        result[currency] = result.get(currency, 0) + round5(total * share)
    return result


def main():
    techs = parse()
    ranges = tier_scale(techs)
    by_discipline = collections.defaultdict(list)
    for tech in techs:
        by_discipline[tech['discipline']].append(tech)

    out = io.StringIO()
    out.write("""# SPDX-FileCopyrightText: 2025 IS14
#
# SPDX-License-Identifier: AGPL-3.0-or-later

# Compatibility prices for the UPSTREAM technology tree — see Docs/_IS14/research-design.md.
# The IS14 tree itself lives in technologies.yml / tech_data.yml; this file only matters for a
# console whose branches include an upstream discipline.
#
# The prototype id IS the technology id. Technologies missing from this table fall back to
# their upstream `cost` divided by 100 and charged as scientific data, so nothing breaks
# when upstream adds a technology.
#
# Generated from the upstream prices, then rescaled and split:
#   tier 1 — one currency, 15-40      (a shift's worth of one department's work)
#   tier 2 — two currencies, 70/30    (50-120)
#   tier 3 — three currencies, 55/30/15 (150-300; nothing at the top is single-currency)
#
# Hand edits are expected and welcome: this file is the balance knob for the whole tree.
""")

    for discipline in sorted(by_discipline):
        out.write('\n# ' + '-' * 74 + '\n')
        out.write('# %s\n' % discipline)
        out.write('# ' + '-' * 74 + '\n')

        for tech in sorted(by_discipline[discipline], key=lambda t: (t['tier'], t['id'])):
            costs = costs_for(tech, ranges)
            out.write('\n- type: is14Technology\n')
            out.write('  id: %s\n' % tech['id'])
            out.write('  costs:\n')
            for currency, amount in costs.items():
                out.write('    %s: %d\n' % (currency, amount))

    io.open('Resources/Prototypes/_IS14/Research/costs.yml', 'w',
            encoding='utf-8', newline='\n').write(out.getvalue())

    print('technologies:', len(techs))
    show = ('SalvageWeapons', 'ExplosiveTechnology', 'BasicRobotics', 'AdvancedRobotics',
            'Cloning', 'BluespaceTimeManipulation', 'PortableMicrofusionWeaponry',
            'IS14ModsuitAdvanced', 'Hydroponics', 'AdvancedAtmospherics')
    for tech in techs:
        if tech['id'] in show:
            print(tech['id'], 't%d' % tech['tier'], dict(costs_for(tech, ranges)))


if __name__ == '__main__':
    main()
