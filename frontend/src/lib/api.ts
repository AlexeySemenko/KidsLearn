const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

type RequestPayload = Record<string, unknown> | null

type RequestOptions = RequestInit & {
  headers?: Record<string, string>
}

type ApiError = Error & {
  status?: number
  payload?: unknown
}

type ReportQueryRange = {
  from?: string
  to?: string
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(options.headers ?? {}),
    },
  })

  if (response.status === 204) {
    return null as T
  }

  const contentType = response.headers.get('content-type') ?? ''
  const payload = contentType.includes('application/json') ? await response.json() : null

  if (!response.ok) {
    const error = payload?.error ?? payload?.title ?? `Request failed with status ${response.status}`
    const requestError: ApiError = new Error(error)
    requestError.status = response.status
    requestError.payload = payload
    throw requestError
  }

  return payload as T
}

function withAuth(accessToken: string, options: RequestOptions = {}): RequestOptions {
  return {
    ...options,
    headers: {
      ...(options.headers ?? {}),
      Authorization: `Bearer ${accessToken}`,
    },
  }
}

export async function loginParent(credentials: RequestPayload) {
  return request('/api/v1/auth/login', {
    method: 'POST',
    body: JSON.stringify(credentials),
  })
}

export async function loginChild(credentials: RequestPayload) {
  return request('/api/v1/auth/child-login', {
    method: 'POST',
    body: JSON.stringify(credentials),
  })
}

export async function refreshSession(refreshToken: string) {
  return request('/api/v1/auth/refresh', {
    method: 'POST',
    body: JSON.stringify({ refreshToken }),
  })
}

export async function revokeSession(refreshToken: string) {
  return request('/api/v1/auth/revoke', {
    method: 'POST',
    body: JSON.stringify({ refreshToken }),
  })
}

export async function getChildren(accessToken: string) {
  return request('/api/v1/children', withAuth(accessToken))
}

export async function createChild(accessToken: string, payload: RequestPayload) {
  return request('/api/v1/children', withAuth(accessToken, {
    method: 'POST',
    body: JSON.stringify(payload),
  }))
}

export async function updateChild(accessToken: string, childId: string, payload: RequestPayload) {
  return request(`/api/v1/children/${childId}`, withAuth(accessToken, {
    method: 'PATCH',
    body: JSON.stringify(payload),
  }))
}

export async function resetChildAccessCode(accessToken: string, childId: string) {
  return request(`/api/v1/children/${childId}/access-code/reset`, withAuth(accessToken, {
    method: 'POST',
  }))
}

export async function deleteChild(accessToken: string, childId: string) {
  return request(`/api/v1/children/${childId}`, withAuth(accessToken, {
    method: 'DELETE',
  }))
}

export async function getLessons(accessToken: string) {
  return request('/api/v1/lessons', withAuth(accessToken))
}

export async function createLesson(accessToken: string, payload: RequestPayload) {
  return request('/api/v1/lessons', withAuth(accessToken, {
    method: 'POST',
    body: JSON.stringify(payload),
  }))
}

export async function updateLesson(accessToken: string, lessonId: string, payload: RequestPayload) {
  return request(`/api/v1/lessons/${lessonId}`, withAuth(accessToken, {
    method: 'PATCH',
    body: JSON.stringify(payload),
  }))
}

export async function duplicateLesson(accessToken: string, lessonId: string) {
  return request(`/api/v1/lessons/${lessonId}/duplicate`, withAuth(accessToken, {
    method: 'POST',
  }))
}

export async function deleteLesson(accessToken: string, lessonId: string) {
  return request(`/api/v1/lessons/${lessonId}`, withAuth(accessToken, {
    method: 'DELETE',
  }))
}

export async function getAssignments(accessToken: string) {
  return request('/api/v1/assignments', withAuth(accessToken))
}

export async function createAssignment(accessToken: string, payload: RequestPayload) {
  return request('/api/v1/assignments', withAuth(accessToken, {
    method: 'POST',
    body: JSON.stringify(payload),
  }))
}

export async function generateParentAiLesson(accessToken: string, payload: RequestPayload) {
  return request('/api/v1/ai/lessons/generate', withAuth(accessToken, {
    method: 'POST',
    body: JSON.stringify(payload),
  }))
}

export async function editParentAiLesson(accessToken: string, lessonId: string, payload: RequestPayload) {
  return request(`/api/v1/ai/lessons/${lessonId}/edit`, withAuth(accessToken, {
    method: 'POST',
    body: JSON.stringify(payload),
  }))
}

export async function getParentAiLessonRevisions(accessToken: string, lessonId: string) {
  return request(`/api/v1/ai/lessons/${lessonId}/revisions`, withAuth(accessToken))
}

export async function getParentAssignmentForSolving(accessToken: string, assignmentId: string) {
  return request(`/api/v1/assignments/${assignmentId}/for-solving`, withAuth(accessToken))
}

export async function getParentResultDetail(accessToken: string, resultId: string) {
  return request(`/api/v1/results/${resultId}`, withAuth(accessToken))
}

export async function getChildAssignments(accessToken: string) {
  return request('/api/v1/child/assignments', withAuth(accessToken))
}

export async function getChildAssignmentForSolving(accessToken: string, assignmentId: string) {
  return request(`/api/v1/child/assignments/${assignmentId}/for-solving`, withAuth(accessToken))
}

export async function submitChildAssignmentAnswers(accessToken: string, assignmentId: string, payload: RequestPayload) {
  return request(`/api/v1/child/assignments/${assignmentId}/answers`, withAuth(accessToken, {
    method: 'POST',
    body: JSON.stringify(payload),
  }))
}

export async function completeChildAssignment(accessToken: string, assignmentId: string) {
  return request(`/api/v1/child/assignments/${assignmentId}/complete`, withAuth(accessToken, {
    method: 'POST',
  }))
}

export async function getChildResultDetail(accessToken: string, resultId: string) {
  return request(`/api/v1/child/results/${resultId}`, withAuth(accessToken))
}

export async function getParentChildReportSummary(accessToken: string, childId: string, { from, to }: ReportQueryRange = {}) {
  const query = new URLSearchParams()

  if (from) {
    query.set('from', from)
  }

  if (to) {
    query.set('to', to)
  }

  const suffix = query.size > 0 ? `?${query.toString()}` : ''
  return request(`/api/v1/reports/children/${childId}${suffix}`, withAuth(accessToken))
}

export async function exportParentChildReportCsv(accessToken: string, childId: string, { from, to }: ReportQueryRange = {}) {
  const query = new URLSearchParams({ format: 'csv' })

  if (from) {
    query.set('from', from)
  }

  if (to) {
    query.set('to', to)
  }

  const response = await fetch(`${API_BASE}/api/v1/reports/children/${childId}/export?${query.toString()}`, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
  })

  if (!response.ok) {
    const contentType = response.headers.get('content-type') ?? ''
    const payload = contentType.includes('application/json') ? await response.json() : null
    const error = payload?.error ?? payload?.title ?? `Request failed with status ${response.status}`
    const requestError: ApiError = new Error(error)
    requestError.status = response.status
    requestError.payload = payload
    throw requestError
  }

  const fileBlob = await response.blob()
  const contentDisposition = response.headers.get('content-disposition') ?? ''
  const fileNameMatch = contentDisposition.match(/filename\*?=(?:UTF-8''|\")?([^\";]+)/i)

  return {
    fileBlob,
    fileName: fileNameMatch ? decodeURIComponent(fileNameMatch[1].replace(/\"/g, '')) : `child-report-${childId}.csv`,
  }
}