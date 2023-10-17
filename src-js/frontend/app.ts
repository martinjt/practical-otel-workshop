import express, { Request, Express } from 'express';
import http from 'http';

const PORT: number = parseInt(process.env.PORT || '5004');
const app: Express = express();

app.get('/', async (req, res) => {
    const firstname = req.query.firstname;
    const surname = req.query.surname;
    let responseBody = '';
    http.get({
      host: 'localhost',
      port: 5005,
      path: `/profile?firstname=${firstname}&surname=${surname}`,
    }, (response:any) => {
      var body = "";
      response.on('data', (chunk:never) => body += chunk);
      response.on('end', () => {
        responseBody = body;
        var json:PersonInfo = JSON.parse(responseBody) as PersonInfo;
        res.send(`hello ${json.name} you're ${json.age} years old`);
      });
    });

});

app.listen(PORT, () => {
  console.log(`Listening for requests on http://localhost:${PORT}`);
});

class PersonInfo {
    name: string = '';
    age: number = 0;
}