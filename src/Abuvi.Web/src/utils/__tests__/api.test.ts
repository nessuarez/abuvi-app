import { describe, it, expect } from 'vitest'
import { api } from '../api'

describe('API Configuration', () => {
  it('should have correct base URL', () => {
    expect(api.defaults.baseURL).toBeDefined()
  })

  it('should have JSON content type header', () => {
    expect(api.defaults.headers['Content-Type']).toBe('application/json')
  })
})
