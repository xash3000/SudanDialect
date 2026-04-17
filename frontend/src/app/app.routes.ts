import { Routes } from '@angular/router';
import { HomePageComponent } from './pages/home-page/home-page.component';
import { ContactPageComponent } from './pages/contact-page/contact-page.component';
import { AboutPageComponent } from './pages/about-page/about-page.component';
import { BrowsePageComponent } from './pages/browse-page/browse-page.component';
import { NotFoundPageComponent } from './pages/not-found-page/not-found-page.component';

export const routes: Routes = [
  {
    path: '',
    component: HomePageComponent
  },
  {
    path: 'word/:id',
    component: HomePageComponent
  },
  {
    path: 'browse',
    component: BrowsePageComponent
  },
  {
    path: 'contact',
    component: ContactPageComponent
  },
  {
    path: 'about',
    component: AboutPageComponent
  },
  {
    path: 'admin',
    loadChildren: () => import('./admin/admin.routes').then((module) => module.adminRoutes)
  },
  {
    path: '**',
    component: NotFoundPageComponent
  }
];
