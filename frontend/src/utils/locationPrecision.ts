/**
 * How trustworthy a camp's coordinates are, derived from the Google place types
 * stored in `Camp.placeTypes`.
 *
 * Geocoding a camp by name usually lands on the municipality rather than the site
 * itself, so this exists to make that visible and reviewable: an editor can sort
 * the list and go straight to the ones still pointing at a whole town.
 */
export type PrecisionLevel = 'exact' | 'area' | 'town' | 'unknown' | 'missing'

export interface LocationPrecision {
	level: PrecisionLevel
	label: string
	/** PrimeVue Tag severity. */
	severity: 'success' | 'info' | 'warn' | 'danger' | 'secondary'
	/** True when the point is good enough to stop reviewing it. */
	isPrecise: boolean
}

/**
 * Google types that identify an actual venue rather than an administrative area.
 *
 * Deliberately excludes "establishment" and "point_of_interest": Google attaches
 * those to almost anything, including a whole valley, so they prove nothing about
 * precision.
 */
const EXACT_TYPES = ["campground", "rv_park", "lodging", "tourist_attraction", "premise"];

/** A named natural place: better than a town, still not the campsite itself. */
const AREA_TYPES = ["natural_feature", "park"];

/** Administrative areas: the coordinates are a town centroid. */
const TOWN_TYPES = [
	"locality",
	"sublocality",
	"political",
	"postal_code",
	"route"
];

const PRECISION: Record<PrecisionLevel, Omit<LocationPrecision, "level">> = {
	exact: { label: "Ubicación exacta", severity: "success", isPrecise: true },
	area: { label: "Paraje natural", severity: "info", isPrecise: false },
	town: { label: "Municipio", severity: "warn", isPrecise: false },
	unknown: { label: "Sin clasificar", severity: "secondary", isPrecise: false },
	missing: { label: "Sin ubicación", severity: "danger", isPrecise: false }
};

const build = (level: PrecisionLevel): LocationPrecision => ({
	level,
	...PRECISION[level]
});

/**
 * Classifies a camp's coordinates.
 *
 * Types are ranked rather than counted: Google returns several at once
 * ("establishment point_of_interest" or "locality political"), and the most
 * specific one is what tells us how good the point is.
 */
export function getLocationPrecision(
	placeTypes: string | null | undefined,
	latitude: number | null | undefined,
	longitude: number | null | undefined
): LocationPrecision {
	if (
		latitude === null ||
		latitude === undefined ||
		longitude === null ||
		longitude === undefined
	) {
		return build("missing");
	}

	if (!placeTypes || !placeTypes.trim()) return build("unknown");

	const types = placeTypes
		.split(/[\s,]+/)
		.map((t) => t.trim().toLowerCase())
		.filter(Boolean);

	if (types.some((t) => EXACT_TYPES.includes(t))) return build("exact");
	if (types.some((t) => AREA_TYPES.includes(t))) return build("area");
	if (types.some((t) => TOWN_TYPES.includes(t))) return build("town");

	return build("unknown");
}

/** Sort helper: least precise first, which is the review order that matters. */
export const PRECISION_ORDER: Record<PrecisionLevel, number> = {
	missing: 0,
	town: 1,
	unknown: 2,
	area: 3,
	exact: 4
};
