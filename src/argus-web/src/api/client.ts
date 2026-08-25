import type { ValidationProblem } from './types';

function getApiBaseUrl(): string {
  const baseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5056';

  return baseUrl.replace(/\/$/, '');
}

export class ApiError extends Error {
  public readonly status: number;

  public readonly details?: ValidationProblem;

  constructor(status: number, message: string, details?: ValidationProblem) {
    super(message);
    this.status = status;
    this.details = details;
  }
}

function toReadableMessage(status: number, details?: ValidationProblem): string {
  if (details?.errors) {
    const firstError = Object.values(details.errors).flat()[0];
    if (firstError) {
      return firstError;
    }
  }

  if (details?.detail) {
    return details.detail;
  }

  if (details?.title) {
    return details.title;
  }

  if (status === 404) {
    return 'Requested resource was not found.';
  }

  if (status >= 500) {
    return 'ARGUS could not complete the request due to a server error.';
  }

  return 'Request failed.';
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${getApiBaseUrl()}${path}`, {
    ...init,
    headers: {
      ...(init?.headers ?? {}),
    },
  });

  if (!response.ok) {
    let details: ValidationProblem | undefined;
    try {
      details = (await response.json()) as ValidationProblem;
    } catch {
      details = undefined;
    }

    throw new ApiError(response.status, toReadableMessage(response.status, details), details);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}
