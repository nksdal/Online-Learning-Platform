import { Routes } from '@angular/router';
import { CatalogPage } from './pages/catalog.page';
import { studentGuard } from './core/student.guard';
import { CoursesPage, HowItWorksPage } from './pages/info.pages';
import { adminGuard, changePasswordGuard } from './core/admin.guard';
import { teacherGuard } from './core/teacher.guard';
import { authenticatedGuard } from './core/authenticated.guard';

export const routes: Routes = [
  { path: '', component: CatalogPage, title: 'Nikladori · Learn with purpose' },
  { path: 'courses', component: CoursesPage, title: 'Explore courses · Nikladori' },
  { path: 'how-it-works', component: HowItWorksPage, title: 'How it works · Nikladori' },
  { path: 'account', loadComponent: () => import('./pages/account.page').then(module => module.AccountPage), canActivate: [authenticatedGuard], title: 'Your account · Nikladori' },
  { path: 'courses/:id', loadComponent: () => import('./pages/course-detail.page').then(module => module.CourseDetailPage), title: 'Course · Nikladori' },
  { path: 'teacher', loadComponent: () => import('./pages/teacher.page').then(module => module.TeacherDashboardPage), canActivate: [teacherGuard], title: 'Teacher studio · Nikladori' },
  { path: 'admin', loadComponent: () => import('./pages/admin.page').then(module => module.AdminDashboardPage), canActivate: [adminGuard], title: 'Admin workspace · Nikladori' },
  { path: 'my-learning', loadComponent: () => import('./pages/student.pages').then(module => module.StudentDashboardPage), canActivate: [studentGuard], title: 'My Learning · Nikladori' },
  { path: 'learn/:id', loadComponent: () => import('./pages/student.pages').then(module => module.LearningPage), canActivate: [studentGuard], title: 'Course lessons · Nikladori' },
  { path: 'login', loadComponent: () => import('./pages/auth.pages').then(module => module.LoginPage), title: 'Sign in · Nikladori' },
  { path: 'change-password', loadComponent: () => import('./pages/auth.pages').then(module => module.ChangePasswordPage), canActivate: [changePasswordGuard], title: 'Update password · Nikladori' },
  { path: 'register', loadComponent: () => import('./pages/auth.pages').then(module => module.RegisterPage), title: 'Create account · Nikladori' },
  { path: 'verify-email', loadComponent: () => import('./pages/auth.pages').then(module => module.VerifyEmailPage), title: 'Confirm email · Nikladori' },
  { path: 'forgot-password', loadComponent: () => import('./pages/auth.pages').then(module => module.ForgotPasswordPage), title: 'Reset password · Nikladori' },
  { path: 'reset-password', loadComponent: () => import('./pages/auth.pages').then(module => module.ResetPasswordPage), title: 'Choose a new password · Nikladori' },
  { path: '**', redirectTo: '' },
];