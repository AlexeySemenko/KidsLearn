import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  editParentAiLesson,
  exportParentChildReportCsv,
  getParentAiLessonRevisions,
  getParentChildReportSummary,
} from './api'

afterEach(() => {
  vi.restoreAllMocks()
})

describe('api route wiring', () => {
  it('calls child report summary endpoint without parent prefix', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      status: 200,
      headers: {
        get: () => 'application/json',
      },
      json: async () => ({ childId: 'child-1' }),
    }))

    vi.stubGlobal('fetch', fetchMock)

    await getParentChildReportSummary('token-1', 'child-1', {
      from: '2026-06-01T00:00:00.000Z',
      to: '2026-06-07T23:59:59.999Z',
    })

    const [url, options] = fetchMock.mock.calls[0]
    expect(url).toContain('/api/v1/reports/children/child-1')
    expect(url).not.toContain('/api/v1/parent/reports')
    expect(url).toContain('from=2026-06-01T00%3A00%3A00.000Z')
    expect(url).toContain('to=2026-06-07T23%3A59%3A59.999Z')
    expect(options.headers.Authorization).toBe('Bearer token-1')
  })

  it('calls csv export endpoint and returns parsed filename', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      status: 200,
      headers: {
        get: (header) => {
          if (header === 'content-disposition') {
            return 'attachment; filename="report.csv"'
          }

          return null
        },
      },
      blob: async () => new Blob(['a,b,c']),
    }))

    vi.stubGlobal('fetch', fetchMock)

    const result = await exportParentChildReportCsv('token-2', 'child-2')

    const [url, options] = fetchMock.mock.calls[0]
    expect(url).toContain('/api/v1/reports/children/child-2/export?format=csv')
    expect(url).not.toContain('/api/v1/parent/reports')
    expect(options.headers.Authorization).toBe('Bearer token-2')
    expect(result.fileName).toBe('report.csv')
  })

  it('calls ai lesson edit endpoint with lesson id', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      status: 200,
      headers: {
        get: () => 'application/json',
      },
      json: async () => ({ revisionNumber: 1 }),
    }))

    vi.stubGlobal('fetch', fetchMock)

    await editParentAiLesson('token-3', 'lesson-1', {
      command: 'change-difficulty',
      params: { difficulty: 'Hard' },
      answers: null,
    })

    const [url, options] = fetchMock.mock.calls[0]
    expect(url).toContain('/api/v1/ai/lessons/lesson-1/edit')
    expect(options.method).toBe('POST')
    expect(options.headers.Authorization).toBe('Bearer token-3')
  })

  it('calls ai lesson revisions endpoint with lesson id', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      status: 200,
      headers: {
        get: () => 'application/json',
      },
      json: async () => ([]),
    }))

    vi.stubGlobal('fetch', fetchMock)

    await getParentAiLessonRevisions('token-4', 'lesson-9')

    const [url, options] = fetchMock.mock.calls[0]
    expect(url).toContain('/api/v1/ai/lessons/lesson-9/revisions')
    expect(options.headers.Authorization).toBe('Bearer token-4')
  })
})
