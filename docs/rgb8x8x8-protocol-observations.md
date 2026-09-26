# RGB 8×8×8 protocol observations

This document records black-box experiments against the physical cube. It distinguishes facts known before testing, direct physical observations, derived relationships, and remaining hypotheses. It must not be populated from legacy encoder code, old mapping/palette fixtures, or inherited firmware source.

## Known independently before testing

- Serial connection: 115200 baud.
- A complete visible RGB frame is transmitted as two packets.
- A packet is four header bytes (00 FF 00 00), followed by a plane byte (00 or 01), followed by 192 payload bytes.
- Each packet is 197 bytes; a two-plane frame is 394 bytes.
- There is no checksum, acknowledgement, handshake, or readback.
- The physical cube contains 8×8×8 RGB voxels.

Before the experiments below, voxel ordering, axis ordering/orientation, byte ordering, bit ordering, RGB channel order, plane weighting, and color quantization were unknown.

## Experiment performed

The standalone protocol probe sent black before each trial and then set one selected payload bit in plane 0, plane 1, or both planes. Walking-bit trials covered adjacent bytes, channel boundaries, row/height boundaries, and the final payload bit.

Plane behavior was compared by activating the same voxel/channel in:

1. Plane 0 only.
2. Plane 1 only.
3. Both planes simultaneously.

The plane comparison was also observed with a fast camera shutter to expose multiplex/PWM behavior.

## Physical observations

These are direct observations from the physical cube. Coordinates use the convention (height, width, depth).

| packet plane | byte | bit | observed voxel | observed color | brightness/notes |
|:---:|---:|---:|---|---|---|
| 0 | 0 | 0 | (1,1,1) | green | Single-plane activation |
| 0 | 0 | 1 | (1,1,2) | green | Single-plane activation |
| 0 | 0 | 7 | (1,1,8) | green | Single-plane activation |
| 0 | 1 | 0 | (1,2,1) | green | Single-plane activation |
| 0 | 2 | 0 | (1,1,1) | blue | Single-plane activation |
| 0 | 3 | 0 | (1,2,1) | blue | Single-plane activation |
| 0 | 4 | 0 | (1,1,1) | red | Single-plane activation |
| 0 | 5 | 0 | (1,2,1) | red | Single-plane activation |
| 0 | 6 | 0 | (1,3,1) | green | Single-plane activation |
| 0 | 7 | 0 | (1,4,1) | green | Single-plane activation |
| 0 | 23 | 0 | (1,8,1) | red | End of first-height byte group |
| 0 | 24 | 0 | (2,1,1) | green | Start of second-height byte group |
| 0 | 191 | 7 | (8,8,8) | red | Final payload byte and bit |
| 1 | 191 | 7 | (8,8,8) | red | Same mapping as plane 0 |

Additional direct plane observations:

- Plane 0 and plane 1 address the same voxel and color channel for a selected byte and bit.
- Either plane alone produces approximately the same apparent brightness.
- Activating the same channel in both planes produces approximately twice the apparent brightness.
- Fast-shutter observation was consistent with equal-weight multiplex/PWM contributions from the two planes.

## Derived hypothesis and validation

The walking-bit observations suggested a regular mapping:

- Bits advance through physical depth.
- Adjacent byte pairs select alternating physical widths.
- Six bytes cover green, blue, and red for one pair of widths.
- A 24-byte group covers all eight widths at one physical height.

This relationship was checked at internal transitions and endpoints, including bytes 0–7, 23–24, and byte 191/bit 7. Those confirmation samples matched the predicted spatial and RGB results. The spatial/RGB relationship is therefore promoted from hypothesis to confirmed mapping below.

The equal-weight plane model remains an experimentally supported interpretation of apparent brightness and fast-shutter behavior. It should be used as the working model unless later hardware evidence contradicts it.

## Confirmed mapping

### Coordinate convention and physical orientation

Coordinates are written as (height, width, depth), with each coordinate ranging from 1 through 8.

- (1,1,1) is the front-bottom-right voxel.
- Physical depth increases from front to back.
- Physical width increases from right to left.
- Physical height increases from bottom to top.

### Payload bit and byte mapping

For a voxel at (height, width, depth):

~~~text
bit = depth - 1

byte =
    24 * (height - 1)
  + 6  * floor((width - 1) / 2)
  + channelOffset
  + ((width - 1) % 2)
~~~

Channel offsets are:

| channel | channelOffset |
|---|---:|
| green | 0 |
| blue | 2 |
| red | 4 |

Thus payload bits 0 through 7 map to physical depths 1 through 8. Within each six-byte width-pair group, green occupies offsets 0–1, blue occupies offsets 2–3, and red occupies offsets 4–5. The even/odd term selects the first or second physical width in that pair.

### Packet-plane mapping

Plane 0 and plane 1 use the same confirmed byte/bit-to-voxel and RGB-channel mapping.

### Plane weighting and brightness

Status: experimentally supported working model.

The two planes appear to be equal-weight additive brightness planes:

- Plane 0 alone: approximately one unit of apparent brightness.
- Plane 1 alone: approximately one unit of apparent brightness.
- Both planes: approximately two units of apparent brightness.

This conclusion is supported by direct apparent-brightness comparison and fast-shutter observation of the multiplex/PWM behavior. It is not being elevated beyond the available hardware evidence; later contradictory measurements should revise this section.

### Palette or quantization

The payload's spatial mapping and primary RGB channel selection are confirmed. No broader production color palette or quantization rule has been inferred from these experiments.

## Confirmation samples

| packet plane | byte | bit | predicted voxel/color | observed voxel/color | result |
|:---:|---:|---:|---|---|---|
| 0 | 0 | 7 | green (1,1,8) | green (1,1,8) | match |
| 0 | 6 | 0 | green (1,3,1) | green (1,3,1) | match |
| 0 | 23 | 0 | red (1,8,1) | red (1,8,1) | match |
| 0 | 24 | 0 | green (2,1,1) | green (2,1,1) | match |
| 0 | 191 | 7 | red (8,8,8) | red (8,8,8) | match |
| 1 | 191 | 7 | red (8,8,8) | red (8,8,8) | match |

## Remaining questions

- Whether additional hardware tests reveal any nonlinearity beyond the observed equal-weight two-plane behavior.
- Whether production color quantization requires rules not established by these mapping experiments.
