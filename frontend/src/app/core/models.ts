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

export interface CourseProgress {
  courseId: string;
  courseTitle: string;
  courseStatus: string;
  totalLessons: number;
  completedLessons: number;
  completionPercentage: number;
  lessons: LessonProgress[];
}

export interface LessonProgress {
  lessonId: string;
  title: string;
  position: number;
  isCompleted: boolean;
  completedAtUtc?: string | null;
}

export interface Lesson {
  id: string;
  courseId: string;
  title: string;
  content: string;
  position: number;
}

export interface LearningStreak {
  timeZoneId: string;
  currentStreakDays: number;
  longestStreakDays: number;
  learnedToday: boolean;
  lastLearningDayLocal?: string | null;
  recentLearningDays: string[];
}

export interface RecommendedCourse {
  courseId: string;
  title: string;
  summary: string;
  categoryId: string;
  enrollmentCount: number;
  reason: string;
}

export interface Assignment {
  id: string;
  courseId: string;
  title: string;
  instructions: string;
  dueAtUtc?: string | null;
  maxAttempts: number;
  maxScore: number;
}

export interface AssignmentSubmission {
  id: string;
  assignmentId: string;
  attemptNumber: number;
  text: string;
  submittedAtUtc: string;
  score?: number | null;
  feedback?: string | null;
  gradedAtUtc?: string | null;
}

export interface TeacherAssignmentSubmission extends AssignmentSubmission {
  studentUserId: string;
  gradedByUserId?: string | null;
}

export interface DiscussionPost {
  id: string;
  courseId: string;
  authorUserId: string;
  parentPostId?: string | null;
  body: string;
  isHidden: boolean;
  moderationReason?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface TokenResponse {
  accessToken: string;
  expiresAtUtc: string;
}

export interface MessageResponse {
  message: string;
}
