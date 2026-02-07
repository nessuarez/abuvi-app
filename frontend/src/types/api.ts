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
