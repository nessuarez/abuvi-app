import type { UserRole } from '@/types/user'

const ROLE_LABELS: Record<string, string> = {
  Admin: 'Administrador',
  Board: 'Junta Directiva',
  Member: 'Socio',
}

export const getRoleLabel = (role: UserRole | string): string => {
  return ROLE_LABELS[role] ?? role
}
