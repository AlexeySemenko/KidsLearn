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

export interface AuthUser {
  id: string
  role: 'Parent' | 'Child' | 'Admin'
  email: string
  displayName?: string | null
}

export interface AdminUser {
  id: string
  email: string
  displayName: string | null
  role: string
  emailVerified: boolean
  externalProvider: string | null
  createdAt: string
  lastAccessAt: string | null
}

export interface AdminCreateUserRequest {
  email: string
  displayName?: string | null
  role: string
}

export interface AdminUpdateUserRequest {
  displayName?: string | null
  role?: string | null
}

export interface AuthSessionResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
  user: AuthUser
}

export interface ParentLoginRequest {
  email: string
  password: string
}

export interface ChildLoginRequest {
  childId: string
  accessCode: string
}

export interface ChildSummary {
  id: string
  name: string
  grade: number
  accessCode?: string | null
}

export interface LessonsListResponse {
  items: LessonSummary[]
  totalCount?: number
  page?: number
  pageSize?: number
}

export interface LessonSummary {
  id: string
  title: string
  subject: string
  grade: number
  topic: string
  difficulty: string
  createdAt: string
  questionCount: number
  createdByName?: string | null
}

export interface LessonAnswerOption {
  id: string
  answerText: string
  isCorrect: boolean
  order: number
}

export interface LessonQuestion {
  id: string
  questionText: string
  explanation: string
  order: number
  answers: LessonAnswerOption[]
}

export interface LessonDetail {
  id: string
  title: string
  subject: string
  grade: number
  topic: string
  difficulty: string
  createdAt: string
  questions: LessonQuestion[]
}

export interface AssignmentResponse {
  id: string
  childId: string
  childName?: string | null
  lessonId: string
  lessonTitle?: string | null
  lessonSubject?: string | null
  assignedAt: string
  dueDate?: string | null
  status: string
  resultId?: string | null
  score?: number | null
  assignedByName?: string | null
}

export interface AiAnswerOption {
  id: string
  answerText: string
  isCorrect: boolean
  order: number
}

export interface AiQuestion {
  id: string
  questionText: string
  explanation: string
  order: number
  answers: AiAnswerOption[]
}

export interface AiLessonDraft {
  id: string
  title: string
  subject: string
  grade: number
  topic: string
  difficulty: string
  createdAt: string
  questions: AiQuestion[]
}

export interface AiProviderMeta {
  provider: string
  model: string
  fallbackUsed: boolean
  note?: string | null
}

export interface GenerateAiLessonRequest {
  subject: string
  grade: number
  topic: string
  questionCount: number
  difficulty?: string | null
  language?: string | null
  questionTypes?: string[] | null
}

export interface GenerateAiLessonResponse {
  createdLessonId: string
  lessonDraft: AiLessonDraft
  providerMeta: AiProviderMeta
}

export interface EditAiAnswerInput {
  answerText: string
  isCorrect: boolean
}

export interface EditAiLessonRequest {
  command: string
  params?: Record<string, string> | null
  answers?: EditAiAnswerInput[] | null
}

export interface EditAiLessonResponse {
  revisionId: string
  revisionNumber: number
  diffSummary: string
  lessonDraft: AiLessonDraft
}

export interface AiLessonRevisionSummary {
  revisionId: string
  revisionNumber: number
  diffSummary: string
  createdAt: string
}

export interface ChildReportSummary {
  childId: string
  completionRate: number
  averageScore: number
  solvedCount: number
  streakDays: number
}

export interface CsvExportResult {
  fileBlob: Blob
  fileName: string
}

export interface LinkedParent {
  parentId: string
  email: string
  linkedAt: string
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

export async function loginParent(credentials: ParentLoginRequest): Promise<AuthSessionResponse> {
  return request<AuthSessionResponse>('/api/v1/auth/login', {
    method: 'POST',
    body: JSON.stringify(credentials),
  })
}

export function getParentGoogleStartUrl(returnPath = '/parent'): string {
  const normalizedPath = returnPath.startsWith('/') && !returnPath.startsWith('//')
    ? returnPath
    : '/parent'

  const searchParams = new URLSearchParams({ returnPath: normalizedPath })
  return `${API_BASE}/api/v1/auth/google/start?${searchParams.toString()}`
}

export async function finalizeGoogleParentAuth(authCode: string): Promise<AuthSessionResponse> {
  return request<AuthSessionResponse>('/api/v1/auth/google/finalize', {
    method: 'POST',
    body: JSON.stringify({ authCode }),
  })
}

export function getChildGoogleStartUrl(returnPath = '/child'): string {
  const normalizedPath = returnPath.startsWith('/') && !returnPath.startsWith('//')
    ? returnPath
    : '/child'

  const searchParams = new URLSearchParams({ returnPath: normalizedPath })
  return `${API_BASE}/api/v1/auth/child/google/start?${searchParams.toString()}`
}

export async function finalizeGoogleChildAuth(authCode: string): Promise<AuthSessionResponse> {
  return request<AuthSessionResponse>('/api/v1/auth/child/google/finalize', {
    method: 'POST',
    body: JSON.stringify({ authCode }),
  })
}

export async function loginChild(credentials: ChildLoginRequest): Promise<AuthSessionResponse> {
  return request<AuthSessionResponse>('/api/v1/auth/child-login', {
    method: 'POST',
    body: JSON.stringify(credentials),
  })
}

export async function refreshSession(refreshToken: string): Promise<AuthSessionResponse> {
  return request<AuthSessionResponse>('/api/v1/auth/refresh', {
    method: 'POST',
    body: JSON.stringify({ refreshToken }),
  })
}

export async function revokeSession(refreshToken: string): Promise<null> {
  return request<null>('/api/v1/auth/revoke', {
    method: 'POST',
    body: JSON.stringify({ refreshToken }),
  })
}

export async function getChildren(accessToken: string): Promise<ChildSummary[]> {
  return request<ChildSummary[]>('/api/v1/children', withAuth(accessToken))
}

export async function createChild(accessToken: string, payload: RequestPayload) {
  return request('/api/v1/children', withAuth(accessToken, {
    method: 'POST',
    body: JSON.stringify(payload),
  }))
}

export async function createChildWithGmail(accessToken: string, payload: RequestPayload) {
  return request('/api/v1/children/with-gmail', withAuth(accessToken, {
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

export async function getLessons(accessToken: string): Promise<LessonsListResponse> {
  return request<LessonsListResponse>('/api/v1/lessons', withAuth(accessToken))
}

export async function getLesson(accessToken: string, lessonId: string): Promise<LessonDetail> {
  return request<LessonDetail>(`/api/v1/lessons/${lessonId}`, withAuth(accessToken))
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

export async function getAssignments(accessToken: string): Promise<AssignmentResponse[]> {
  return request<AssignmentResponse[]>('/api/v1/assignments', withAuth(accessToken))
}

export async function createAssignment(accessToken: string, payload: RequestPayload): Promise<AssignmentResponse> {
  return request<AssignmentResponse>('/api/v1/assignments', withAuth(accessToken, {
    method: 'POST',
    body: JSON.stringify(payload),
  }))
}

export async function generateParentAiLesson(accessToken: string, payload: GenerateAiLessonRequest): Promise<GenerateAiLessonResponse> {
  return request<GenerateAiLessonResponse>('/api/v1/ai/lessons/generate', withAuth(accessToken, {
    method: 'POST',
    body: JSON.stringify(payload),
  }))
}

export async function editParentAiLesson(accessToken: string, lessonId: string, payload: EditAiLessonRequest): Promise<EditAiLessonResponse> {
  return request<EditAiLessonResponse>(`/api/v1/ai/lessons/${lessonId}/edit`, withAuth(accessToken, {
    method: 'POST',
    body: JSON.stringify(payload),
  }))
}

export async function getParentAiLessonRevisions(accessToken: string, lessonId: string): Promise<AiLessonRevisionSummary[]> {
  return request<AiLessonRevisionSummary[]>(`/api/v1/ai/lessons/${lessonId}/revisions`, withAuth(accessToken))
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

export interface ResultBreakdownAnswer {
  answerId: string
  answerText: string
  isCorrect: boolean
}

export interface ResultBreakdownItem {
  questionId: string
  questionText: string
  correct: boolean
  selectedAnswerOptionId: string | null
  answers: ResultBreakdownAnswer[]
}

export interface ResultDetail {
  resultId: string
  assignmentId: string
  lessonTitle: string
  score: number
  completedAt: string
  correctAnswers: number
  totalQuestions: number
  breakdown: ResultBreakdownItem[]
}

export interface ResultListItem {
  resultId: string
  assignmentId: string
  lessonTitle: string
  subject: string
  topic: string
  grade: number
  score: number
  completedAt: string
  correctAnswers: number
  totalQuestions: number
}

export async function getChildResults(accessToken: string): Promise<ResultListItem[]> {
  return request<ResultListItem[]>('/api/v1/child/results', withAuth(accessToken))
}

export async function getChildResultDetail(accessToken: string, resultId: string): Promise<ResultDetail> {
  return request<ResultDetail>(`/api/v1/child/results/${resultId}`, withAuth(accessToken))
}

export async function getParentChildReportSummary(accessToken: string, childId: string, { from, to }: ReportQueryRange = {}): Promise<ChildReportSummary> {
  const query = new URLSearchParams()

  if (from) {
    query.set('from', from)
  }

  if (to) {
    query.set('to', to)
  }

  const suffix = query.size > 0 ? `?${query.toString()}` : ''
  return request<ChildReportSummary>(`/api/v1/reports/children/${childId}${suffix}`, withAuth(accessToken))
}

export async function exportParentChildReportCsv(accessToken: string, childId: string, { from, to }: ReportQueryRange = {}): Promise<CsvExportResult> {
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

export async function getAdminUsers(accessToken: string): Promise<AdminUser[]> {
  return request<AdminUser[]>('/api/v1/admin/users', withAuth(accessToken))
}

export interface AdminCreateUserResponse {
  user: AdminUser
  emailSent: boolean
}

export async function createAdminUser(accessToken: string, payload: AdminCreateUserRequest): Promise<AdminCreateUserResponse> {
  return request<AdminCreateUserResponse>('/api/v1/admin/users', withAuth(accessToken, {
    method: 'POST',
    body: JSON.stringify(payload),
  }))
}

export async function updateAdminUser(accessToken: string, userId: string, payload: AdminUpdateUserRequest): Promise<AdminUser> {
  return request<AdminUser>(`/api/v1/admin/users/${userId}`, withAuth(accessToken, {
    method: 'PATCH',
    body: JSON.stringify(payload),
  }))
}

export async function deleteAdminUser(accessToken: string, userId: string): Promise<null> {
  return request<null>(`/api/v1/admin/users/${userId}`, withAuth(accessToken, {
    method: 'DELETE',
  }))
}

export async function getParentChildResults(accessToken: string, childId: string): Promise<ResultListItem[]> {
  return request<ResultListItem[]>(`/api/v1/children/${childId}/results`, withAuth(accessToken))
}

export async function getLinkedParents(accessToken: string): Promise<LinkedParent[]> {
  return request<LinkedParent[]>('/api/v1/manage/linked-parents', withAuth(accessToken))
}

export interface LinkParentAccountResponse {
  linkedParent: LinkedParent
  emailSent: boolean
}

export async function linkParentAccount(accessToken: string, email: string): Promise<LinkParentAccountResponse> {
  return request<LinkParentAccountResponse>('/api/v1/manage/linked-parents', withAuth(accessToken, {
    method: 'POST',
    body: JSON.stringify({ email }),
  }))
}

export async function unlinkParentAccount(accessToken: string, linkedParentId: string): Promise<null> {
  return request<null>(`/api/v1/manage/linked-parents/${linkedParentId}`, withAuth(accessToken, {
    method: 'DELETE',
  }))
}