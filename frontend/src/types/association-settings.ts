// Association settings types for global configuration

export interface AgeRangeSettings {
  babyMaxAge: number
  childMinAge: number
  childMaxAge: number
  adultMinAge: number
}

export interface UpdateAgeRangesRequest extends AgeRangeSettings {}
