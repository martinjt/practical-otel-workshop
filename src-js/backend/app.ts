import express, { Request, Express } from 'express';
import { trace } from '@opentelemetry/api';

const PORT: number = parseInt(process.env.PORT || '5005');
const app: Express = express();

app.get('/profile', (req, res) => {
    const span = trace.getActiveSpan();
    const age = Math.floor(Math.random() * 100);
    span?.setAttribute('age', age);
  res.send({
    name: `${req.query.firstname} ${req.query.surname}`,
    age
  })
});

app.listen(PORT, () => {
  console.log(`Listening for requests on http://localhost:${PORT}`);
});