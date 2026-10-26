// The island view (ADR-076): an island as the game's world generator made it for a seed (the
// exporter's samples), drawn with three.js. Heights, biomes and fresh water are the game's; the
// colours are biome_palette.json's, with sand by the sea, rock on steep slopes and snow on the
// heights as the game's own ground has them. Villages, holy sites and Creatures are labelled.

import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';

const VERTICAL = 1.6;
const SKY = 0x1b2944;

/** Loads an island: its description and its samples. */
export async function loadIsland(file) {
  const meta = await (await fetch(`islands/${file}`)).json();
  const buffer = await (await fetch(`islands/${meta.samples}`)).arrayBuffer();
  const count = meta.grid * meta.grid;
  return {
    meta,
    heights: new Float32Array(buffer, 0, count),
    water: new Float32Array(buffer, count * 4, count),
    biomes: new Uint8Array(buffer, count * 8, count),
  };
}

/** Whether this browser can draw WebGL. */
export function canDraw() {
  try {
    const canvas = document.createElement('canvas');
    return Boolean(canvas.getContext('webgl2') || canvas.getContext('webgl'));
  } catch {
    return false;
  }
}

function colour(hex) {
  return new THREE.Color(hex);
}

function smooth(edge0, edge1, value) {
  const t = Math.min(1, Math.max(0, (value - edge0) / (edge1 - edge0)));
  return t * t * (3 - 2 * t);
}

// A small, steady variation per sample, so wide land is not flat colour.
function grain(x, z) {
  const s = Math.sin(x * 12.9898 + z * 78.233) * 43758.5453;
  return (s - Math.floor(s)) * 0.08 - 0.04;
}

/** The island in three.js: its land, its sea and its fresh water, centred on the origin. */
function buildIsland(island, palette, step) {
  const { meta, heights, water, biomes } = island;
  const grid = meta.grid;
  const cells = Math.floor((grid - 1) / step);
  const side = cells + 1;
  const size = meta.worldSize;
  const spacing = size / (grid - 1);
  const half = size / 2;
  const sea = meta.seaLevel;
  const at = (gx, gz) => heights[Math.min(grid - 1, gz) * grid + Math.min(grid - 1, gx)];

  const positions = new Float32Array(side * side * 3);
  const colours = new Float32Array(side * side * 3);
  const biomeColours = meta.biomes.map((name) => colour(palette[name] ?? palette.Grassland));
  const seaFloor = colour(palette.SeaFloor);
  const deep = colour(palette.DeepWater);
  const beach = colour(palette.Beach);
  const rock = colour(palette.Rock);
  const snow = colour(palette.Snow);
  const c = new THREE.Color();

  for (let j = 0; j < side; j++) {
    for (let i = 0; i < side; i++) {
      const gx = Math.min(grid - 1, i * step);
      const gz = Math.min(grid - 1, j * step);
      const h = at(gx, gz);
      const v = (j * side + i) * 3;
      positions[v] = gx * spacing - half;
      positions[v + 1] = h * VERTICAL;
      positions[v + 2] = gz * spacing - half;

      if (h < sea) {
        c.copy(seaFloor).lerp(deep, smooth(0, palette.DeepWaterDepth, sea - h));
      } else {
        c.copy(biomeColours[biomes[gz * grid + gx]] ?? biomeColours[1]);
        const slope = Math.hypot(at(gx + step, gz) - at(Math.max(0, gx - step), gz), at(gx, gz + step) - at(gx, Math.max(0, gz - step))) / (2 * step * spacing);
        c.lerp(rock, smooth(palette.RockSlopeStart * 0.5, palette.RockSlopeFull * 0.5, slope));
        c.lerp(beach, 1 - smooth(palette.BeachMaxHeight * 0.4, palette.BeachMaxHeight * 1.2, h - sea));
        c.lerp(snow, smooth(palette.SnowStartHeight, palette.SnowStartHeight + palette.SnowBlendRange, h));
        const g = grain(gx, gz);
        c.r += g;
        c.g += g;
        c.b += g;
      }
      colours[v] = c.r;
      colours[v + 1] = c.g;
      colours[v + 2] = c.b;
    }
  }

  const indices = [];
  for (let j = 0; j < cells; j++) {
    for (let i = 0; i < cells; i++) {
      const a = j * side + i;
      indices.push(a, a + side, a + 1, a + 1, a + side, a + side + 1);
    }
  }

  const land = new THREE.BufferGeometry();
  land.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  land.setAttribute('color', new THREE.BufferAttribute(colours, 3));
  land.setIndex(indices);
  land.computeVertexNormals();

  const group = new THREE.Group();
  group.add(new THREE.Mesh(land, new THREE.MeshLambertMaterial({ vertexColors: true })));

  const seaPlane = new THREE.Mesh(
    new THREE.PlaneGeometry(size * 30, size * 30),
    new THREE.MeshPhongMaterial({ color: colour(palette.ShallowWater), transparent: true, opacity: 0.72, shininess: 80, specular: 0x88aacc }),
  );
  seaPlane.rotation.x = -Math.PI / 2;
  seaPlane.position.y = sea * VERTICAL + 0.2;
  group.add(seaPlane);

  const fresh = freshWater(island, step, spacing, half);
  if (fresh) {
    group.add(new THREE.Mesh(fresh, new THREE.MeshPhongMaterial({ color: colour(palette.ShallowWater).offsetHSL(0, 0.05, 0.08), transparent: true, opacity: 0.9, shininess: 90 })));
  }
  return group;
}

/** Rivers and lakes: the cells where fresh water stands, at its surface. */
function freshWater(island, step, spacing, half) {
  const { meta, heights, water } = island;
  const grid = meta.grid;
  const surface = (gx, gz) => water[Math.min(grid - 1, gz) * grid + Math.min(grid - 1, gx)];
  const positions = [];
  const corner = (gx, gz, fallback) => {
    const w = surface(gx, gz);
    const y = Number.isNaN(w) ? fallback : w + 0.3;
    positions.push(gx * spacing - half, y * VERTICAL, gz * spacing - half);
  };
  for (let gz = 0; gz + step < grid; gz += step) {
    for (let gx = 0; gx + step < grid; gx += step) {
      const corners = [[gx, gz], [gx, gz + step], [gx + step, gz], [gx + step, gz + step]];
      const wet = corners.map(([x, z]) => surface(x, z)).filter((w) => !Number.isNaN(w));
      if (wet.length < 2) {
        continue;
      }
      const low = Math.min(...corners.map(([x, z]) => heights[z * grid + x])) - 1;
      const tri = (p) => corner(corners[p][0], corners[p][1], low);
      [0, 1, 2, 2, 1, 3].forEach(tri);
    }
  }
  if (positions.length === 0) {
    return null;
  }
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geometry.computeVertexNormals();
  return geometry;
}

/** Bilinear height at a world position, for markers and labels. */
function heightAt(island, x, z) {
  const { meta, heights } = island;
  const grid = meta.grid;
  const spacing = meta.worldSize / (grid - 1);
  const fx = Math.min(grid - 1, Math.max(0, x / spacing));
  const fz = Math.min(grid - 1, Math.max(0, z / spacing));
  const x0 = Math.floor(fx);
  const z0 = Math.floor(fz);
  const x1 = Math.min(grid - 1, x0 + 1);
  const z1 = Math.min(grid - 1, z0 + 1);
  const tx = fx - x0;
  const tz = fz - z0;
  const h = (gx, gz) => heights[gz * grid + gx];
  const top = h(x0, z0) + (h(x1, z0) - h(x0, z0)) * tx;
  const bottom = h(x0, z1) + (h(x1, z1) - h(x0, z1)) * tx;
  return Math.max(island.meta.seaLevel, top + (bottom - top) * tz);
}

/**
 * A view of an island in a container: auto-turning and still for the hero, or turned and zoomed by
 * the player in the explorer, with its villages, holy sites and Creatures labelled.
 */
export class IslandView {
  constructor(container, { interactive = false, labels = true, detail = 1, godColors = [], palette, sky = false }) {
    this.container = container;
    this.palette = palette;
    this.godColors = godColors;
    this.detail = detail;
    this.showLabels = labels;
    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: 'high-performance' });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    this.renderer.setClearColor(0x000000, 0);
    container.prepend(this.renderer.domElement);
    this.scene = new THREE.Scene();
    this.scene.fog = new THREE.Fog(SKY, 2400, 6400);
    if (sky) {
      // A sky of the fog's colour, so the far sea melts into it.
      this.scene.background = new THREE.Color(SKY);
    }
    this.camera = new THREE.PerspectiveCamera(38, 1, 10, 20000);
    this.scene.add(new THREE.HemisphereLight(0xc9dcff, 0x4a3a22, 1.25));
    const sun = new THREE.DirectionalLight(0xffe6bf, 2.1);
    sun.position.set(-1400, 1600, 900);
    this.scene.add(sun);
    this.controls = new OrbitControls(this.camera, this.renderer.domElement);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.06;
    this.controls.enablePan = false;
    this.controls.maxPolarAngle = 1.32;
    this.controls.minDistance = 260;
    this.controls.maxDistance = 4200;
    this.controls.autoRotate = !interactive || !matchMedia('(prefers-reduced-motion: reduce)').matches;
    this.controls.autoRotateSpeed = interactive ? 0.25 : 0.45;
    this.controls.enabled = interactive;
    if (interactive) {
      this.renderer.domElement.addEventListener('pointerdown', () => { this.controls.autoRotate = false; });
    }
    if (!interactive && matchMedia('(prefers-reduced-motion: reduce)').matches) {
      this.controls.autoRotate = false;
    }
    this.labelLayer = document.createElement('div');
    this.labelLayer.className = 'island-labels';
    container.append(this.labelLayer);
    this.labels = [];
    this.visible = true;
    this.resize();
    new ResizeObserver(() => this.resize()).observe(container);
    new IntersectionObserver(([entry]) => {
      this.visible = entry.isIntersecting;
      if (this.visible) {
        this.loop();
      }
    }).observe(container);
    document.addEventListener('visibilitychange', () => this.loop());
  }

  resize() {
    const { clientWidth: w, clientHeight: h } = this.container;
    if (w === 0 || h === 0) {
      return;
    }
    this.renderer.setSize(w, h, false);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }

  /** Shows an island, in place of any shown before. */
  show(island) {
    if (this.island) {
      this.scene.remove(this.island);
      this.island.traverse((o) => { o.geometry?.dispose(); o.material?.dispose(); });
    }
    this.data = island;
    this.island = buildIsland(island, this.palette, this.detail);
    this.scene.add(this.island);
    this.placeMarkers(island);
    const size = island.meta.worldSize;
    if (!this.framed) {
      this.camera.position.set(size * 0.62, size * 0.62, size * 0.78);
      this.controls.target.set(0, 40, 0);
      this.framed = true;
    }
    this.controls.update();
    this.loop();
  }

  placeMarkers(island) {
    const half = island.meta.worldSize / 2;
    this.labelLayer.replaceChildren();
    this.labels = [];
    const markers = new THREE.Group();
    const godColor = (id) => this.godColors[(Math.max(1, id) - 1) % this.godColors.length] ?? '#e7e2d6';
    const gods = new Map(island.meta.gods.map((g) => [g.id, g]));
    const add = (x, z, lift, html, kind) => {
      const element = document.createElement('div');
      element.className = `island-label ${kind}`;
      element.innerHTML = html;
      this.labelLayer.append(element);
      this.labels.push({ element, point: new THREE.Vector3(x - half, heightAt(island, x, z) * VERTICAL + lift, z - half) });
    };
    const settlements = [...island.meta.settlements].sort((a, b) => b.population - a.population);
    for (const s of settlements) {
      const god = gods.get(s.god);
      const color = s.god ? godColor(s.god) : '#cfc8b8';
      const pillar = new THREE.Mesh(new THREE.CylinderGeometry(3, 3, 90, 8), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.85 }));
      pillar.position.set(s.x - half, heightAt(island, s.x, s.z) * VERTICAL + 45, s.z - half);
      markers.add(pillar);
      const who = god ? `${god.name} ${Math.round(s.share * 100)}%` : 'no god yet';
      add(s.x, s.z, 100, `<div class="name" style="color:${color}">${escape(s.name)}</div><div class="who">${s.population} people · ${escape(who)}</div>`, 'village');
    }
    for (const site of island.meta.holySites) {
      const stone = new THREE.Mesh(new THREE.ConeGeometry(9, 46, 4), new THREE.MeshLambertMaterial({ color: 0xf4ead2 }));
      stone.position.set(site.x - half, heightAt(island, site.x, site.z) * VERTICAL + 23, site.z - half);
      markers.add(stone);
      add(site.x, site.z, 60, escape(site.name), 'site');
    }
    for (const creature of island.meta.creatures) {
      const god = gods.get(creature.god);
      add(creature.x, creature.z, 30, `${escape(creature.name)}<span class="who"> · ${escape(god?.name ?? '')}'s ${escape(creature.species.toLowerCase())}</span>`, 'creature');
    }
    this.island.add(markers);
  }

  setLabels(on) {
    this.showLabels = on;
    this.labelLayer.style.display = on ? '' : 'none';
  }

  updateLabels() {
    if (!this.showLabels || this.labels.length === 0) {
      return;
    }
    const w = this.container.clientWidth;
    const h = this.container.clientHeight;
    const v = new THREE.Vector3();
    // Labels come in order of importance (villages by size, holy sites, Creatures): one that would
    // cover a more important one waits until the view makes room for it.
    const placed = [];
    for (const label of this.labels) {
      v.copy(label.point).project(this.camera);
      let hidden = v.z > 1 || v.x < -1.1 || v.x > 1.1 || v.y < -1.1 || v.y > 1.1;
      const x = ((v.x + 1) / 2) * w;
      const y = ((1 - v.y) / 2) * h;
      if (!hidden) {
        label.width ??= label.element.offsetWidth;
        label.height ??= label.element.offsetHeight;
        const box = { left: x - label.width / 2, right: x + label.width / 2, top: y - label.height, bottom: y };
        hidden = placed.some((p) => box.left < p.right && box.right > p.left && box.top < p.bottom && box.bottom > p.top);
        if (!hidden) {
          placed.push(box);
        }
      }
      label.element.classList.toggle('hidden', hidden);
      if (!hidden) {
        label.element.style.left = `${x}px`;
        label.element.style.top = `${y}px`;
      }
    }
  }

  loop() {
    if (this.running || !this.visible || document.hidden || !this.island) {
      return;
    }
    this.running = true;
    const frame = () => {
      if (!this.visible || document.hidden) {
        this.running = false;
        return;
      }
      this.controls.update();
      this.renderer.render(this.scene, this.camera);
      this.updateLabels();
      requestAnimationFrame(frame);
    };
    requestAnimationFrame(frame);
  }
}

function escape(text) {
  return String(text).replace(/[&<>"']/g, (ch) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[ch]);
}
