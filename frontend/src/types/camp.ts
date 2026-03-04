// Camp location types matching backend DTOs

export interface SharedRoomInfo {
	quantity: number;
	bedsPerRoom: number;
	hasBathroom: boolean;
	hasShower: boolean;
	notes?: string | null;
}

export interface AccommodationCapacity {
	privateRoomsWithBathroom?: number | null;
	privateRoomsSharedBathroom?: number | null;
	sharedRooms?: SharedRoomInfo[] | null;
	bungalows?: number | null;
	campOwnedTents?: number | null;
	memberTentAreaSquareMeters?: number | null;
	memberTentCapacityEstimate?: number | null;
	motorhomeSpots?: number | null;
	notes?: string | null;

	// CSV-imported fields
	totalCapacity?: number | null;
	roomsDescription?: string | null;
	bungalowsDescription?: string | null;
	tentsDescription?: string | null;
	tentAreaDescription?: string | null;
	parkingSpots?: number | null;
	hasAdaptedMenu?: boolean | null;
	hasEnclosedDiningRoom?: boolean | null;
	hasSwimmingPool?: boolean | null;
	hasSportsCourt?: boolean | null;
	hasForestArea?: boolean | null;
}

export interface Camp {
	id: string;
	name: string;
	description: string | null;
	location: string | null;
	latitude: number | null;
	longitude: number | null;
	googlePlaceId: string | null;
	// Lightweight extended fields (present in list AND detail)
	formattedAddress: string | null;
	phoneNumber: string | null;
	websiteUrl: string | null;
	googleMapsUrl: string | null;
	googleRating: number | null;
	googleRatingCount: number | null;
	businessStatus: string | null;
	pricePerAdult: number;
	pricePerChild: number;
	pricePerBaby: number;
	isActive: boolean;
	createdAt: string;
	updatedAt: string;
	editionCount?: number; // For display in list view
	accommodationCapacity?: AccommodationCapacity | null;
	calculatedTotalBedCapacity?: number | null;

	// Extra fields
	province: string | null;
	contactEmail: string | null;
	contactPerson: string | null;
	contactCompany: string | null;
	secondaryWebsiteUrl: string | null;
	basePrice: number | null;
	vatIncluded: boolean | null;
	externalSourceId: number | null;
	abuviManagedByUserId: string | null;
	abuviContactedAt: string | null;
	abuviPossibility: string | null;
	abuviLastVisited: string | null;
	abuviHasDataErrors: boolean | null;
	lastModifiedByUserId: string | null;
}

export interface CampPlacesPhoto {
	id: string;
	photoReference: string | null;
	photoUrl: string | null;
	width: number;
	height: number;
	attributionName: string;
	attributionUrl: string | null;
	isPrimary: boolean;
	displayOrder: number;
}

export interface CampDetailResponse extends Camp {
	// Full address breakdown (detail-only)
	streetAddress: string | null;
	locality: string | null;
	administrativeArea: string | null;
	postalCode: string | null;
	country: string | null;
	nationalPhoneNumber: string | null;
	// Metadata
	placeTypes: string | null;
	lastGoogleSyncAt: string | null;
	// Photos from Google Places
	photos: CampPlacesPhoto[];
}

export interface CreateCampRequest {
	name: string;
	description: string | null;
	location: string | null;
	latitude: number | null;
	longitude: number | null;
	googlePlaceId: string | null;
	pricePerAdult: number;
	pricePerChild: number;
	pricePerBaby: number;
	accommodationCapacity?: AccommodationCapacity | null;

	// Extra writable fields
	province?: string | null;
	contactEmail?: string | null;
	contactPerson?: string | null;
	contactCompany?: string | null;
	secondaryWebsiteUrl?: string | null;
	basePrice?: number | null;
	vatIncluded?: boolean | null;
	abuviManagedByUserId?: string | null;
	abuviContactedAt?: string | null;
	abuviPossibility?: string | null;
	abuviLastVisited?: string | null;
	abuviHasDataErrors?: boolean | null;
}

export interface UpdateCampRequest extends CreateCampRequest {
	isActive: boolean;
}

export interface CampLocation {
	latitude: number;
	longitude: number;
	name: string;
	year?: number;
	location?: string;
	lastEditionYear?: number;
}

export interface CampObservation {
	id: string;
	campId: string;
	text: string;
	season: string | null;
	createdByUserId: string | null;
	createdAt: string;
}

export interface CampAuditLogEntry {
	id: string;
	fieldName: string;
	oldValue: string | null;
	newValue: string | null;
	changedByUserId: string;
	changedAt: string;
}

export interface AddCampObservationRequest {
	text: string;
	season: string | null;
}
