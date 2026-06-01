import { describe, expect, it } from 'vitest'
import { formatTaskNumberPreview, validateTaskNumberPattern } from './task-number-format'

describe('task number format', () => {
  const sampleDate = new Date(Date.UTC(2026, 4, 28))

  it('renders supported tokens', () => {
    expect(formatTaskNumberPreview('BP{N}{Date}', 1, sampleDate)).toBe('BP1280526')
    expect(formatTaskNumberPreview('BP{N:000}{Date}', 7, sampleDate)).toBe('BP007280526')
    expect(formatTaskNumberPreview('TASK-{N:00}-{Date:yyyyMMdd}', 12, sampleDate)).toBe('TASK-12-20260528')
    expect(formatTaskNumberPreview('TASK_{N}_{Date:dd-MM-yyyy}', 3, sampleDate)).toBe('TASK_3_28-05-2026')
  })

  it('validates URL-safe patterns', () => {
    expect(validateTaskNumberPattern('BP{N}{Date}')).toHaveLength(0)
    expect(validateTaskNumberPattern('BP/{N}{Date}')).toContain('Texto literal pode conter apenas letras, numeros, _ e -.')
    expect(validateTaskNumberPattern('BP{N}{Date:yyyy/MM}')).toContain('Use apenas dd, MM, yy e yyyy no formato de data.')
    expect(validateTaskNumberPattern('BP{N}{Date:MMMM}')).toContain('Use apenas dd, MM, yy e yyyy no formato de data.')
    expect(validateTaskNumberPattern('BP{N}{Date:dddd}')).toContain('Use apenas dd, MM, yy e yyyy no formato de data.')
    expect(validateTaskNumberPattern('BP{Date}')).toContain('Inclua {N}.')
    expect(validateTaskNumberPattern('BP{N}')).toContain('Inclua {Date}.')
  })
})
