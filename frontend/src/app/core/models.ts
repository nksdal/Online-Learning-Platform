export interface Course {
  id: string;
  categoryId: string;
  instructorUserId: string;
  title: string;
  summary: string;
  description: string;
  status: string;
  rejectionReason?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface Category {
  id: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

export interface UserProfile {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  timeZoneId: string;
  roles: string[];
}

export interface Enrollment {
  id: string;
  courseId: string;
  courseTitle: string;
  courseSummary: string;
  courseStatus: string;
  enrolledAtUtc: string;
}

export interface TokenResponse {
  accessToken: string;
  expiresAtUtc: string;
}

export interface MessageResponse {
  message: string;
}
