import { describe, it, expect } from 'vitest'
import { getRoleLabel } from '@/utils/user'

describe('getRoleLabel', () => {
  it('should return "Administrador" for Admin role', () => {
    expect(getRoleLabel('Admin')).toBe('Administrador')
  })

  it('should return "Junta Directiva" for Board role', () => {
    expect(getRoleLabel('Board')).toBe('Junta Directiva')
  })

  it('should return "Socio" for Member role', () => {
    expect(getRoleLabel('Member')).toBe('Socio')
  })

  it('should return the original value for unknown roles', () => {
    expect(getRoleLabel('Unknown')).toBe('Unknown')
  })
})
