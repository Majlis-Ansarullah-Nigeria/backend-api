import { describe, expect, it } from "vitest";
import { mapStep, schematicLayout } from "./map";

describe("mapStep", () => {
  it("puts compliance in fixed quarters", () => {
    expect(mapStep(0, [], true)).toBe(0);
    expect(mapStep(49, [], true)).toBe(1);
    expect(mapStep(74, [], true)).toBe(2);
    expect(mapStep(100, [], true)).toBe(3);
  });

  it("places figures between the lowest and highest on screen", () => {
    const values = [10, 40, 70, 100, null];
    expect(values.map((v) => mapStep(v, values, false))).toEqual([0, 1, 2, 3, null]);
    expect(mapStep(5, [5, 5], false)).toBe(3);
  });
});

describe("schematicLayout", () => {
  it("places located tiles north-west to south-east", () => {
    const tiles = [
      { id: "nw", latitude: 12, longitude: 5 },
      { id: "se", latitude: 5, longitude: 8 },
      { id: "sw", latitude: 7, longitude: 3.5 },
    ];

    const cells = schematicLayout(tiles);
    const at = (id: string) => cells.find((c) => c.item.id === id)!;

    expect(at("nw").row).toBeLessThan(at("se").row);
    expect(at("sw").column).toBe(0);
    expect(at("se").column).toBe(2);
  });

  it("gives every tile its own cell, including unlocated ones", () => {
    const tiles = [
      { id: "a", latitude: 7, longitude: 3 },
      { id: "b", latitude: 7, longitude: 3 },
      { id: "c", latitude: null, longitude: null },
    ];

    const cells = schematicLayout(tiles);

    expect(new Set(cells.map((c) => `${c.row}:${c.column}`)).size).toBe(3);
  });

  it("is a plain grid without locations", () => {
    const tiles = ["a", "b", "c", "d"].map((id) => ({ id, latitude: null, longitude: null }));

    expect(schematicLayout(tiles).map((c) => [c.row, c.column])).toEqual([[0, 0], [0, 1], [0, 2], [1, 0]]);
  });
});
