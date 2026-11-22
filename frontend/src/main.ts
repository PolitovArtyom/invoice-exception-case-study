import { createApp } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import PrimeVue from 'primevue/config'
import Aura from '@primeuix/themes/aura'
import App from './App.vue'
import CaseListPage from './pages/CaseListPage.vue'
import CasePage from './pages/CasePage.vue'
import './style.css'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: CaseListPage },
    { path: '/cases/:caseId', name: 'case', component: CasePage, props: true },
  ],
  scrollBehavior: () => ({ top: 0 }),
})

createApp(App)
  .use(PrimeVue, {
    theme: { preset: Aura, options: { darkModeSelector: false } },
  })
  .use(router)
  .mount('#app')
