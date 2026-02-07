// Matches backend ApiResponse structure from Abuvi.API/Common/Models/ApiResponse.cs
export interface ApiResponse<T> {
  success: boolean
  data: T | null
  error: ApiError | null
}

export interface ApiError {
  message: string
  code: string
  details?: Array<{ field: string; message: string }>
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
  hasNextPage: boolean
  hasPreviousPage: boolean
}
