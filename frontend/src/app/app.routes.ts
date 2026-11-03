import { Routes } from '@angular/router';
import { CatalogPage } from './pages/catalog.page';
import { CourseDetailPage } from './pages/course-detail.page';
import { ForgotPasswordPage, LoginPage, RegisterPage, ResetPasswordPage, VerifyEmailPage } from './pages/auth.pages';
import { LearningPage, StudentDashboardPage } from './pages/student.pages';
import { studentGuard } from './core/student.guard';
import { CoursesPage, HowItWorksPage } from './pages/info.pages';
import { TeacherDashboardPage } from './pages/teacher.page';
import { teacherGuard } from './core/teacher.guard';

export const routes: Routes = [
  { path: '', component: CatalogPage, title: 'Nikladori · Learn with purpose' },
  { path: 'courses', component: CoursesPage, title: 'Explore courses · Nikladori' },
  { path: 'how-it-works', component: HowItWorksPage, title: 'How it works · Nikladori' },
  { path: 'courses/:id', component: CourseDetailPage, title: 'Course · Nikladori' },
  { path: 'teacher', component: TeacherDashboardPage, canActivate: [teacherGuard], title: 'Teacher studio · Nikladori' },
  { path: 'my-learning', component: StudentDashboardPage, canActivate: [studentGuard], title: 'My Learning · Nikladori' },
  { path: 'learn/:id', component: LearningPage, canActivate: [studentGuard], title: 'Course lessons · Nikladori' },
  { path: 'login', component: LoginPage, title: 'Sign in · Nikladori' },
  { path: 'register', component: RegisterPage, title: 'Create account · Nikladori' },
  { path: 'verify-email', component: VerifyEmailPage, title: 'Confirm email · Nikladori' },
  { path: 'forgot-password', component: ForgotPasswordPage, title: 'Reset password · Nikladori' },
  { path: 'reset-password', component: ResetPasswordPage, title: 'Choose a new password · Nikladori' },
  { path: '**', redirectTo: '' },
];
