"""Converts a BYOND .dmi icon file into an SS14 .rsi directory.

A .dmi is a PNG spritesheet whose zTXt "Description" chunk describes the states; an .rsi is a
directory with a meta.json and one PNG per state. Both lay icons out row-major, but they order
them differently inside a state: .dmi is frame-major (all directions of frame 1, then frame 2),
.rsi is direction-major (all frames of south, then north). This script transposes that.

Usage:
    python Tools/_IS14/dmi_to_rsi.py <input.dmi> <output.rsi> [--states a,b,c] [--copyright "..."]

Only the states named with --states are converted, which is usually what you want: a tg machine
icon file carries dozens of unrelated states.
"""
import argparse
import json
import os
import struct
import zlib

from PIL import Image

# BYOND direction order inside a .dmi state, which is also RSI's order for the first four.
DIR_COUNTS = (1, 4, 8)


def read_dmi_description(path):
    """Pulls the '# BEGIN DMI' metadata block out of a PNG's tEXt/zTXt chunks."""
    with open(path, 'rb') as handle:
        data = handle.read()

    if data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError('%s is not a PNG' % path)

    offset = 8
    while offset < len(data):
        (length,) = struct.unpack('>I', data[offset:offset + 4])
        kind = data[offset + 4:offset + 8]
        payload = data[offset + 8:offset + 8 + length]
        offset += 12 + length

        if kind not in (b'tEXt', b'zTXt'):
            if kind == b'IEND':
                break
            continue

        keyword, _, rest = payload.partition(b'\x00')

        if keyword != b'Description':
            continue

        if kind == b'tEXt':
            return rest.decode('utf-8', 'replace')

        # zTXt: one compression-method byte, then a zlib stream.
        return zlib.decompress(rest[1:]).decode('utf-8', 'replace')

    raise ValueError('%s has no DMI description chunk' % path)


def parse_states(description):
    """Returns (cell_width, cell_height, [state dicts]) in sheet order."""
    width = height = 32
    states = []
    current = None

    for raw in description.splitlines():
        line = raw.strip()

        if not line or line.startswith('#'):
            continue

        key, _, value = line.partition('=')
        key = key.strip()
        value = value.strip()

        if key == 'width':
            width = int(value)
        elif key == 'height':
            height = int(value)
        elif key == 'state':
            current = {
                'name': value.strip('"'),
                'dirs': 1,
                'frames': 1,
                'delays': None,
                'movement': False,
            }
            states.append(current)
        elif current is None:
            continue
        elif key == 'dirs':
            current['dirs'] = int(value)
        elif key == 'frames':
            current['frames'] = int(value)
        elif key == 'delay':
            current['delays'] = [float(part) for part in value.split(',')]
        elif key == 'movement':
            current['movement'] = value == '1'

    return width, height, states


def icon_at(sheet, index, cell_w, cell_h):
    columns = sheet.width // cell_w
    x = (index % columns) * cell_w
    y = (index // columns) * cell_h
    return sheet.crop((x, y, x + cell_w, y + cell_h))


def convert(dmi_path, rsi_path, wanted, copyright_line, license_line):
    description = read_dmi_description(dmi_path)
    cell_w, cell_h, states = parse_states(description)
    sheet = Image.open(dmi_path).convert('RGBA')

    os.makedirs(rsi_path, exist_ok=True)

    cursor = 0
    meta_states = []
    converted = []

    for state in states:
        count = state['dirs'] * state['frames']
        start = cursor
        cursor += count

        if wanted and state['name'] not in wanted:
            continue

        if state['dirs'] not in DIR_COUNTS:
            print('skipping %r: %d directions is not an RSI direction count'
                  % (state['name'], state['dirs']))
            continue

        dirs = state['dirs']
        frames = state['frames']

        # .dmi is frame-major, .rsi is direction-major.
        ordered = []
        for direction in range(dirs):
            for frame in range(frames):
                ordered.append(start + frame * dirs + direction)

        out = Image.new('RGBA', (cell_w * len(ordered), cell_h))
        for position, index in enumerate(ordered):
            out.paste(icon_at(sheet, index, cell_w, cell_h), (position * cell_w, 0))

        # A state name with a slash cannot be a file name; RSI states never have one anyway.
        file_name = state['name'].replace('/', '_') or 'unnamed'
        out.save(os.path.join(rsi_path, file_name + '.png'))

        entry = {'name': state['name']}

        if dirs > 1:
            entry['directions'] = dirs

        if frames > 1:
            # BYOND delays are in tenths of a second, RSI delays are in seconds.
            per_frame = state['delays'] or [1.0] * frames
            per_frame = [round(value / 10.0, 3) for value in per_frame[:frames]]
            entry['delays'] = [list(per_frame) for _ in range(dirs)]

        meta_states.append(entry)
        converted.append('%s (%dx%d frames)' % (state['name'], dirs, frames))

    if not meta_states:
        raise SystemExit('nothing converted — check the state names against the .dmi')

    meta = {
        'version': 1,
        'license': license_line,
        'copyright': copyright_line,
        'size': {'x': cell_w, 'y': cell_h},
        'states': meta_states,
    }

    with open(os.path.join(rsi_path, 'meta.json'), 'w', encoding='utf-8', newline='\n') as handle:
        json.dump(meta, handle, indent=2, ensure_ascii=False)
        handle.write('\n')

    print('%s -> %s' % (dmi_path, rsi_path))
    for line in converted:
        print('  ' + line)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('dmi')
    parser.add_argument('rsi')
    parser.add_argument('--states', default='',
                        help='comma-separated state names; default converts every state')
    parser.add_argument('--copyright', default='Taken from BandaStation (tgstation fork)')
    parser.add_argument('--license', default='CC-BY-SA-3.0')
    args = parser.parse_args()

    wanted = {name for name in args.states.split(',') if name} if args.states else None
    convert(args.dmi, args.rsi, wanted, args.copyright, args.license)


if __name__ == '__main__':
    main()
