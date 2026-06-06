const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

async function request(path, options = {}) {
  const response = await fetch(`${API_BASE}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(options.headers ?? {}),
    },
    ...options,
  })

  if (response.status === 204) {
    return null
  }

  const contentType = response.headers.get('content-type') ?? ''
  const payload = contentType.includes('application/json') ? await response.json() : null

  if (!response.ok) {
    const error = payload?.error ?? payload?.title ?? `Request failed with status ${response.status}`
    const requestError = new Error(error)
    requestError.status = response.status
    requestError.payload = payload
    throw requestError
  }

  return payload
}

function withAuth(accessToken, options = {}) {
  return {
    ...options,
    headers: {
      ...(options.headers ?? {}),
      Authorization: `Bearer ${accessToken}`,
    },
  }
}

export async function loginParent(credentials) {
  return request('/api/v1/auth/login', {
    method: 'POST',
    body: JSON.stringify(credentials),
  })
}

export async function loginChild(credentials) {
  return request('/api/v1/auth/child-login', {
    method: 'POST',
    body: JSON.stringify(credentials),
  })
}

export async function refreshSession(refreshToken) {
  return request('/api/v1/auth/refresh', {
    method: 'POST',
    body: JSON.stringify({ refreshToken }),
  })
}

export async function revokeSession(refreshToken) {
  return request('/api/v1/auth/revoke', {
    method: 'POST',
    body: JSON.stringify({ refreshToken }),
  })
}

export async function getChildren(accessToken) {
  return request('/api/v1/children', withAuth(accessToken))
}

export async function createChild(accessToken, payload) {
  return request('/api/v1/children', withAuth(accessToken, {
    method: 'POST',
    body: JSON.stringify(payload),
  }))
}

export async function resetChildAccessCode(accessToken, childId) {
  return request(`/api/v1/children/${childId}/access-code/reset`, withAuth(accessToken, {
    method: 'POST',
  }))
}

export async function deleteChild(accessToken, childId) {
  return request(`/api/v1/children/${childId}`, withAuth(accessToken, {
    method: 'DELETE',
  }))
}
